using System;
using GeminiAssistant.Models;
using Microsoft.Gaming.XboxGameBar;
using Windows.UI.Core;

namespace GeminiAssistant.Services
{
    public sealed class GameContextService : IDisposable
    {
        private readonly XboxGameBarWidget _widget;
        private readonly XboxGameBarAppTargetTracker _tracker;
        private readonly CoreDispatcher _uiDispatcher;
        private bool _targetEventsHooked;

        public event EventHandler<GameContextInfo> ContextChanged;

        public GameContextInfo Current { get; private set; } = new GameContextInfo();

        public GameContextService(XboxGameBarWidget widget, CoreDispatcher uiDispatcher)
        {
            _widget = widget ?? throw new ArgumentNullException(nameof(widget));
            _uiDispatcher = uiDispatcher ?? throw new ArgumentNullException(nameof(uiDispatcher));
            _tracker = new XboxGameBarAppTargetTracker(widget);
            _tracker.SettingChanged += OnTrackerSettingChanged;
            Refresh();
            HookTargetChangedIfEnabled();
        }

        private void OnTrackerSettingChanged(XboxGameBarAppTargetTracker sender, object args)
        {
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

        private void Refresh()
        {
            if (_uiDispatcher.HasThreadAccess)
            {
                RefreshCore();
                return;
            }

            _ = _uiDispatcher.RunAsync(CoreDispatcherPriority.Normal, RefreshCore);
        }

        private void RefreshCore()
        {
            var info = new GameContextInfo
            {
                TrackingEnabled = _tracker.Setting == XboxGameBarAppTargetSetting.Enabled
            };

            try
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
            catch
            {
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
