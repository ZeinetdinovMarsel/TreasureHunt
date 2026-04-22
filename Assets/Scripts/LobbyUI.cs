using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

public class LobbyUI : MonoBehaviour
{
    [Inject] private LobbyManager _lobby;
    [Inject] private GameFlowManager _gameFlowManager;
    [Inject] private NetworkServer _networkServer;

    [SerializeField] private GameObject _panel;
    [SerializeField] private TMP_Text _labelText;
    [SerializeField] private TMP_Text _redText;
    [SerializeField] private TMP_Text _blueText;
    [SerializeField] private TMP_Text _readyText;
    [SerializeField] private Button _startGameBtn;

    private void Start()
    {
        _lobby.Red
            .Subscribe(x => _redText.text = $"<color={(x >= 1 ? "green" : "red")}>Команда красных: {IsPlayerConnected(x >= 1)}</color>")
            .AddTo(this);

        _lobby.Blue
            .Subscribe(x => _blueText.text = $"<color={(x >= 1 ? "green" : "red")}>Команда синих: {IsPlayerConnected(x >= 1)}</color>")
            .AddTo(this);

        _lobby.Ready
            .Subscribe(x =>
            {
                _startGameBtn.gameObject.SetActive(x == 2);
                _readyText.text = $" {(x < 2 ? "Начало игры, ожидание игроков..." : "Все игроки подключены, ожидание начала игры...")}";
            }
            )
            .AddTo(this);

        _startGameBtn.onClick.AddListener(() =>
        {
            switch (_gameFlowManager.State.Value)
            {
                case GameState.Lobby:
                    _gameFlowManager.StartMatch();
                    break;
                case GameState.Finished:
                    _gameFlowManager.ResetMatch();
                    _networkServer.ResetGame();
                    _gameFlowManager.StartMatch();

                    break;
            }
            
        });

        _gameFlowManager.State
            .Subscribe(v =>
            {
                switch (v)
                {
                    case GameState.Lobby:
                        _labelText.text = v.ToString();
                        _panel.SetActive(true);
                        break;
                    case GameState.InGame:
                        _panel.SetActive(false);
                        break;
                    case GameState.Finished:
                        _labelText.text = _gameFlowManager.GetWinnerName();
                        _panel.SetActive(true);
                        break;
                }
            })
            .AddTo(this);

        _panel.ObserveEveryValueChanged(p => p.activeSelf)
            .Subscribe(isActive =>
            {
                Time.timeScale = isActive ? 0 : 1;
                Cursor.lockState = isActive ? CursorLockMode.None : CursorLockMode.Locked;
                Cursor.visible = isActive;
            })
            .AddTo(this);
    }

    string IsPlayerConnected(bool connected) => connected ? "Подключена" : "Отсутствует";

}