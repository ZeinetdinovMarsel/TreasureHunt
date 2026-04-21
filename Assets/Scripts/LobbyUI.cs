using TMPro;
using UniRx;
using UnityEngine;
using Zenject;

public class LobbyUI : MonoBehaviour
{
    [Inject] private LobbyManager _lobby;

    [SerializeField] private TMP_Text _redText;
    [SerializeField] private TMP_Text _blueText;
    [SerializeField] private TMP_Text _readyText;

    private void Start()
    {
        _lobby.Red
            .Subscribe(x => _redText.text = $"Red: {x}")
            .AddTo(this);

        _lobby.Blue
            .Subscribe(x => _blueText.text = $"Blue: {x}")
            .AddTo(this);

        _lobby.Ready
            .Subscribe(x => _readyText.text = $"Ready: {x}")
            .AddTo(this);
    }
}