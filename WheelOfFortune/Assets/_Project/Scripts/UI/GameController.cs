using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using WheelOfFortune.Core;

namespace WheelOfFortune.UI
{
    /// <summary>
    /// Connects the pure game state (WheelSession) to the views. Views only display; all decisions happen here
    /// or in WheelSession.
    /// </summary>
    public sealed class GameController : MonoBehaviour
    {
        private const string SpinButtonName = "ui_button_spin";
        private const string ExitButtonName = "ui_button_exit";

        [SerializeField] private GameSettings _settings;
        [SerializeField] private WheelView _wheel;
        [SerializeField] private ZoneBarView _zoneBar;
        [SerializeField] private RewardPanelView _rewardPanel;
        [SerializeField] private CashOutPanelView _cashOutPanel;
        [SerializeField] private ResultPopupView _popup;
        [SerializeField] private ResultPopupView _receiptPopup;
        [SerializeField] private PopupContent _bombPopup;
        [SerializeField] private PopupContent _collectedPopup;
        [SerializeField] private Button _spinButton;
        [SerializeField] private Button _exitButton;

        private WheelSession _session;
        private bool _isSpinning;

        private void Awake()
        {
            _session = new WheelSession(_settings, max => Random.Range(0, max));
            _spinButton.onClick.AddListener(Spin);
            _exitButton.onClick.AddListener(Leave);
            _popup.Closed += Restart;
            if (_receiptPopup == null)
            {
                // Scenes built before the receipt existed: keep working with the plain popup until the scene is rebuilt.
                Debug.LogWarning("Receipt popup is not assigned. Run Tools > Wheel Of Fortune > Refresh Presentation.", this);
                _receiptPopup = _popup;
            }
            else _receiptPopup.Closed += Restart;
        }

        private void OnDestroy()
        {
            _spinButton.onClick.RemoveListener(Spin);
            _exitButton.onClick.RemoveListener(Leave);
            _popup.Closed -= Restart;
            if (_receiptPopup != _popup) _receiptPopup.Closed -= Restart;
        }

        private void Start() => Refresh();

        private void Spin()
        {
            if (_isSpinning) return;
            SetSpinning(true);

            int index = _session.PickSlice();
            _wheel.SpinTo(index, () => OnSpinFinished(index));
        }

        private void OnSpinFinished(int index)
        {
            Vector3 iconPosition = _wheel.GetSliceIconPosition(index);
            RewardDefinition reward = _session.Resolve(index);

            if (reward.IsBomb)
            {
                _rewardPanel.Clear();    // mirror the model: rewards are gone the moment the bomb hits
                _popup.Show(_bombPopup); // input stays locked until the popup restarts the game
                return;
            }

            _rewardPanel.Add(reward, _session.Rewards[reward], iconPosition, () =>
            {
                Refresh();
                SetSpinning(false);
            });
        }

        private void Leave()
        {
            if (_isSpinning || !_session.CanLeave) return;
            SetSpinning(true);

            // Snapshot the totals so the receipt is independent of the session, which is reset when it closes.
            var receipt = new List<KeyValuePair<RewardDefinition, int>>(_session.Rewards);
            _receiptPopup.Show(_collectedPopup, receipt);
        }

        private void Restart()
        {
            _session.Restart();
            _rewardPanel.Clear();
            Refresh();
            SetSpinning(false);
        }

        private void Refresh()
        {
            _wheel.Show(_session);
            _zoneBar.Show(_session.Zone, _settings);
            _cashOutPanel.Show(_session.CanLeave, GameSettings.NextZone(_session.Zone, _settings.SafeZoneInterval));
            SetSpinning(_isSpinning);
        }

        private void SetSpinning(bool value)
        {
            _isSpinning = value;
            _spinButton.interactable = !value;
            _exitButton.interactable = !value && _session.CanLeave;
            _wheel.SetSpinReady(!value);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            _spinButton = UIUtils.FindButton(this, SpinButtonName);
            _exitButton = UIUtils.FindButton(this, ExitButtonName);
        }
#endif
    }
}
