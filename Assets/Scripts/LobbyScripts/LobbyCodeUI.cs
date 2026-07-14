using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using Project.Connection;

public class LobbyCodeUI : NetworkBehaviour
{
    [SerializeField] private TMP_Text _codeText;



    // void Update()
    // {

    // }

    public override void OnGainedOwnership()
    {
        base.OnGainedOwnership();
        if (IsHost)
        {
            _codeText.gameObject.SetActive(true);
            _codeText.text = ConnectionManager.Instance.CurrentJoinCode;
        }else _codeText.gameObject.SetActive(false);
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (IsHost)
        {
            _codeText.gameObject.SetActive(true);
            _codeText.text = ConnectionManager.Instance.CurrentJoinCode;
        }else _codeText.gameObject.SetActive(false);
    }
}
