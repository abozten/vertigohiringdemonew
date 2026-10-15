using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WheelOfFortune.Core;

namespace WheelOfFortune.UI
{
    public sealed class WheelView : MonoBehaviour
    {
        [SerializeField] private RectTransform _rotator;   // child that spins; the root never animates
        [SerializeField] private Image _glow;
        [SerializeField] private float _glowAlpha = 0.16f;
        [SerializeField] private RectTransform _spinPulse;  // parent of the spin button; animated instead of the button itself
        [SerializeField] private Image _baseImage;
        [SerializeField] private Image _indicatorImage;
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private TMP_Text _zoneHintText;
        [SerializeField] private WheelSliceView[] _slices = new WheelSliceView[WheelDefinition.SliceCount];
        [SerializeField] private float _spinDuration = 3.5f;
        [SerializeField] private int _extraTurns = 4;

        private void Start()
        {
            _glow.rectTransform.DOLocalRotate(new Vector3(0f, 0f, -360f), 12f, RotateMode.FastBeyond360)
                .SetEase(Ease.Linear)
                .SetLoops(-1)
                .SetLink(gameObject);
        }

        public Vector3 GetSliceIconPosition(int index) => _slices[index].IconPosition;

        public void Show(WheelSession session)
        {
            WheelDefinition wheel = session.CurrentWheel;
            _baseImage.sprite = wheel.BaseSprite;
            _indicatorImage.sprite = wheel.IndicatorSprite;
            _titleText.text = wheel.Title;
            _titleText.color = wheel.TitleColor;
            Color glow = wheel.TitleColor;
            glow.a = _glowAlpha;
            _glow.color = glow;
            if (_zoneHintText != null)
                _zoneHintText.text = session.CanLeave
                    ? "RISK FREE SPIN  /  COLLECT OR CONTINUE"
                    : "SPIN TO WIN  /  AVOID THE BOMB";

            for (int i = 0; i < _slices.Length; i++)
                _slices[i].Show(wheel.GetSlice(i).Reward, session.GetAmount(i));
        }

        /// <summary>Idle breathing on the spin button while it can be pressed.</summary>
        public void SetSpinReady(bool ready)
        {
            if (_spinPulse == null) return;
            _spinPulse.DOKill();
            _spinPulse.localScale = Vector3.one;
            if (ready)
                _spinPulse.DOScale(1.06f, 0.8f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetLink(gameObject);
        }

        /// <summary>Spins clockwise and stops with slice <paramref name="index"/> under the top indicator.</summary>
        public void SpinTo(int index, Action onComplete)
        {
            // Slice i sits i * 45 degrees clockwise from the top, so rotating the wheel by +i * 45 brings it to the top.
            float sliceAngle = 360f / _slices.Length;
            float target = index * sliceAngle - 360f * _extraTurns;

            _rotator.DOKill();
            _rotator.DOLocalRotate(new Vector3(0f, 0f, target), _spinDuration, RotateMode.FastBeyond360)
                .SetEase(Ease.OutQuart)
                .SetLink(gameObject)
                .OnUpdate(KeepSlicesUpright)
                .OnComplete(() =>
                {
                    KeepSlicesUpright();
                    _indicatorImage.transform.DOPunchRotation(new Vector3(0f, 0f, 15f), 0.3f, 8).SetLink(gameObject);
                    onComplete();
                });
        }

        /// <summary>Slices orbit with the wheel but cancel its rotation, so icons and amounts always read upright.</summary>
        private void KeepSlicesUpright()
        {
            Quaternion upright = Quaternion.Inverse(_rotator.localRotation);
            foreach (WheelSliceView slice in _slices)
                slice.transform.localRotation = upright;
        }
    }
}
