using System;
using GeminiAssistant.Models;
using Microsoft.Gaming.XboxGameBar;

namespace GeminiAssistant.Services
{
    public sealed class GameContextService : IDisposable
    {
        private readonly XboxGameBarWidget _widget;
        private readonly XboxGameBarAppTargetTracker _tracker;
        private bool _targetEventsHooked;

        public event EventHandler<GameContextInfo> ContextChanged;

        public GameContextInfo Current { get; private set; } = new GameContextInfo();

        public GameContextService(XboxGameBarWidget widget)
        {
            _widget = widget ?? throw new ArgumentNullException(nameof(widget));
            _tracker = new XboxGameBarAppTargetTracker(widget);
            _tracker.SettingChanged += OnTrackerSettingChanged;
            Refresh();
            HookTargetChangedIfEnabled();
        }

        private void OnTrackerSettingChanged(XboxGameBarAppTargetTracker sender, object args)
        {
            HookTargetChangedIfEnabled();
            Refresh();
        }

        private void OnTargetChanged(XboxGameBarAppTargetTracker sender, object args)
        {
            Refresh();
        }

        private void HookTargetChangedIfEnabled()
        {
            if (_tracker.Setting == XboxGameBarAppTargetSetting.Enabled)
            {
                if (!_targetEventsHooked)
                {
                    _tracker.TargetChanged += OnTargetChanged;
                    _targetEventsHooked = true;
                }
            }
            else if (_targetEventsHooked)
            {
                _tracker.TargetChanged -= OnTargetChanged;
                _targetEventsHooked = false;
            }
        }

        public void Refresh()
        {
            var info = new GameContextInfo
            {
                TrackingEnabled = _tracker.Setting == XboxGameBarAppTargetSetting.Enabled
            };

            if (info.TrackingEnabled)
            {
                var target = _tracker.GetTarget();
                if (target != null)
                {
                    info.DisplayName = target.DisplayName ?? "Desconocido";
                    info.AumId = target.AumId ?? "";
                    info.TitleId = target.TitleId ?? "";
                    info.IsGame = target.IsGame;
                    info.IsFullscreen = target.IsFullscreen;
                }
            }

            Current = info;
            ContextChanged?.Invoke(this, info);
        }

        public void Dispose()
        {
            _tracker.SettingChanged -= OnTrackerSettingChanged;
            if (_targetEventsHooked)
            {
                _tracker.TargetChanged -= OnTargetChanged;
            }
        }
    }
}
