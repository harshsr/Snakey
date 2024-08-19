using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using JetBrains.Annotations;

public class PlayerController : NetworkBehaviour
{
    [SerializeField] private float speed = 3f;

    [CanBeNull] public static event System.Action GameOverEvent;

    private Camera _mainCamera;
    private Vector3 _mouseInput = Vector3.zero;
    private PlayerLength _playerLength;
    private bool _canCollide = true;

    private readonly ulong[] _targetClientArray = new ulong[1];

    private void Initialize()
    {
        _mainCamera = Camera.main;
        _playerLength = GetComponent<PlayerLength>();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        Initialize();
    }

    private void Update()
    {
        if (!IsOwner || !Application.isFocused) return;
        MovePlayerServer();
    }

    // Server Authoritative Movement
    private void MovePlayerServer()
    {
        _mouseInput.x = Input.mousePosition.x;
        _mouseInput.y = Input.mousePosition.y;
        _mouseInput.z = _mainCamera.nearClipPlane;
        Vector3 mouseWorldCoordinates = _mainCamera.ScreenToWorldPoint(_mouseInput);
        mouseWorldCoordinates.z = 0f;
        MovePlayerServerRpc(mouseWorldCoordinates);
    }

    [ServerRpc]
    private void MovePlayerServerRpc(Vector3 mouseWorldCoordinates)
    {
        transform.position = Vector3.MoveTowards(transform.position,
            mouseWorldCoordinates, Time.deltaTime * speed);

        // Rotate
        if (mouseWorldCoordinates != transform.position)
        {
            Vector3 targetDirection = mouseWorldCoordinates - transform.position;
            targetDirection.z = 0f;
            transform.up = targetDirection;
        }
    }

    // Client Authoritative Movement
    // Not Used
    private void MovePlayerClient()
    {
        /*
        // Movement
        _mouseInput.x = Input.mousePosition.x;
        _mouseInput.y = Input.mousePosition.y;
        _mouseInput.z = _mainCamera.nearClipPlane;
        Vector3 mouseWorldCoordinates = _mainCamera.ScreenToWorldPoint(_mouseInput);
        mouseWorldCoordinates.z = 0f;

        transform.position = Vector3.MoveTowards(transform.position,
            mouseWorldCoordinates, Time.deltaTime * speed);

        // Rotate
        if (mouseWorldCoordinates != transform.position)
        {
            Vector3 targetDirection = mouseWorldCoordinates - transform.position;
            targetDirection.z = 0f;
            transform.up = targetDirection;
        }
        */
    }

    [ServerRpc] // [ServerRpc(RequireOwnership = false)] 
    private void DeterminedCollisionWinnerServerRpc(PlayerData player1, PlayerData player2 ) 
    {
        if (player1.Length > player2.Length)
        {
            WinInformationServerRpc(player1.Id, player2.Id);
        }
        else
        {
            WinInformationServerRpc(player2.Id, player1.Id);
        }
    }

    [ServerRpc] 
    private void WinInformationServerRpc(ulong winner, ulong loser)
    {
        _targetClientArray[0] = winner; 
        ClientRpcParams clientRpcParams = new ClientRpcParams 
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = _targetClientArray 
            }
        };
        AtePlayerClientRpc(clientRpcParams); 

        _targetClientArray[0] = loser;
        clientRpcParams.Send.TargetClientIds = _targetClientArray; 
        GameOverClientRpc(clientRpcParams);
    }

    [ClientRpc]
    private void AtePlayerClientRpc(ClientRpcParams clientRpcParams = default)
    {
        if (!IsOwner) return;
        Debug.Log("You Ate a Player");
    }

    [ClientRpc]
    private void GameOverClientRpc(ClientRpcParams clientRpcParams = default)
    {
        if (!IsOwner) return;
        Debug.Log("you lose");
        GameOverEvent?.Invoke();
        NetworkManager.Singleton.Shutdown();
    }

    private IEnumerator CollisionCheckCouroutine()
    {
        _canCollide = false;
        yield return new WaitForSeconds(0.5f);
        _canCollide = true;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        Debug.Log("Player Collison");
        if (!collision.gameObject.CompareTag("Player")) return;
        if (!IsOwner) return;
        if (!_canCollide) return;

        StartCoroutine(CollisionCheckCouroutine());

        // Head-on Collision
        if (collision.gameObject.TryGetComponent(out PlayerLength playerLength))
        {
            Debug.Log("Head Collision");
            var player1 = new PlayerData()
            {
                Id = OwnerClientId,
                Length = GetComponent<PlayerLength>().length.Value
            };

            var player2 = new PlayerData()
            {
                Id = playerLength.OwnerClientId,
                Length = GetComponent<PlayerLength>().length.Value
            };

            DeterminedCollisionWinnerServerRpc(player1, player2);
        }
        else if (collision.gameObject.TryGetComponent(out Tail tail))
        {
            Debug.Log("Tail Collsion");
            WinInformationServerRpc(tail.networkedOwner 
                .GetComponent<PlayerController>().OwnerClientId, OwnerClientId);
        }
    }

    struct PlayerData : INetworkSerializable
    {
        public ulong Id;
        public ushort Length;

        
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter 
        {
            serializer.SerializeValue(ref Id); 
            serializer.SerializeValue(ref Length);
        }
    }



}
