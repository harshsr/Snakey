﻿using System.Collections.Generic;
using JetBrains.Annotations;
using Unity.Netcode;
using UnityEngine;

public class PlayerLength : NetworkBehaviour
{
    [SerializeField] private GameObject tailPrefab;

   
    public NetworkVariable<ushort> length = new(1, NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    [CanBeNull] public static event System.Action<ushort> ChangedLengthEvent;

    private List<GameObject> _tails;
    private Transform _lastTail;
    private Collider2D _collider2D;

    public override void OnNetworkSpawn() 
    {
        base.OnNetworkSpawn();
        _tails = new List<GameObject>(); // check null
        _lastTail = transform;
        _collider2D = GetComponent<Collider2D>(); // get collider
        if (!IsServer) length.OnValueChanged += LengthChangedEvent; // client updates tails
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        DestroyTails();
    }

    private void DestroyTails()
    {
        while (_tails.Count != 0)
        {
            GameObject tail = _tails[0];
            _tails.RemoveAt(0);
            Destroy(tail);
        }
    }

    // this will be called by the sever
    [ContextMenu("Add Length")]
    public void AddLength()
    {
        length.Value += 1;

        LengthChanged();
    }

    private void LengthChanged()
    {
        InstantiateTail();

        if (!IsOwner) return; 
        ChangedLengthEvent?.Invoke(length.Value);

        ClientMusicPlayer.Instance.PlayNomAudioClip();
    }


    private void LengthChangedEvent(ushort previousValue, ushort newValue)
    {
        Debug.Log("LengthChanged callback");
        LengthChanged();
    }

    private void InstantiateTail()
    {
        GameObject tailGameObject = Instantiate(tailPrefab, transform.position, Quaternion.identity);
        tailGameObject.GetComponent<SpriteRenderer>().sortingOrder = -length.Value;
        if (tailGameObject.TryGetComponent(out Tail tail))
        {
            tail.networkedOwner = transform;
            tail.followTransform = _lastTail;
            _lastTail = tailGameObject.transform;
            Physics2D.IgnoreCollision(tailGameObject.GetComponent<Collider2D>(), _collider2D);   
        }
        _tails.Add(tailGameObject);
    }
}
