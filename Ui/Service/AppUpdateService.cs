using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Timers;
using _1RM.View;
using _1RM.View.Utils;
using Shawn.Utils;
using Shawn.Utils.Wpf;
using Stylet;

namespace _1RM.Service
{
    public class AppUpdateService : NotifyPropertyChangedBase
    {
        private Timer? _checkUpdateTimer;
        private VersionHelper? _checker;

        public string CurrentVersion => AppVersion.Version;
        public string CurrentVersionDate => AppVersion.BuildDate.IndexOf("+", StringComparison.Ordinal) > 0
            ? AppVersion.BuildDate.Substring(0, AppVersion.BuildDate.LastIndexOf("+", StringComparison.Ordinal))
            : AppVersion.BuildDate;

        private string _newVersion = "";
        public string NewVersion
        {
            get => _newVersion;
            private set
            {
                if (SetAndNotifyIfChanged(ref _newVersion, value))
                {
                    RaisePropertyChanged(nameof(HasNewVersion));
                }
            }
        }

        private string _newVersionUrl = "";
        public string NewVersionUrl
        {
            get => _newVersionUrl;
            private set => SetAndNotifyIfChanged(ref _newVersionUrl, value);
        }

        private bool _isBreakingNewVersion;
        public bool IsBreakingNewVersion
        {
            get => _isBreakingNewVersion;
            private set => SetAndNotifyIfChanged(ref _isBreakingNewVersion, value);
        }

        public bool HasNewVersion => string.IsNullOrWhiteSpace(NewVersion) == false;

        public void StartVersionCheckTimer()
        {
            EnsureChecker();
            EnsureTimer();

            if (IoC.Get<ConfigurationService>().General.DoNotCheckNewVersion)
            {
                _checkUpdateTimer?.Stop();
                return;
            }

            _checker!.CheckUpdateAsync();
            _checkUpdateTimer!.Stop();
            _checkUpdateTimer.Start();
        }

        public void CheckUpdateAsync()
        {
            EnsureChecker();
            _checker!.CheckUpdateAsync();
        }

        private void EnsureChecker()
        {
            if (_checker != null)
            {
                return;
            }

            _checker = new VersionHelper(
                AppVersion.VersionData,
                AppVersion.UpdateCheckUrls,
                AppVersion.UpdatePublishUrls,
                customCheckMethod: CustomCheckMethod);
            _checker.OnNewVersionRelease += OnNewVersionRelease;
        }

        private void EnsureTimer()
        {
            if (_checkUpdateTimer != null)
            {
                return;
            }

            _checkUpdateTimer = new Timer
            {
                Interval = 1000 * 60 * 60,
                AutoReset = true,
            };
            _checkUpdateTimer.Elapsed += (_, _) =>
            {
                if (IoC.Get<ConfigurationService>().General.DoNotCheckNewVersion)
                {
                    _checkUpdateTimer?.Stop();
                    return;
                }

                _checker?.CheckUpdateAsync();
            };
        }

        private static VersionHelper.CheckUpdateResult CustomCheckMethod(string html, string publishUrl, VersionHelper.Version currentVersion, VersionHelper.Version? ignoreVersion)
        {
            var ret = VersionHelper.DefaultCheckMethod(html, publishUrl, currentVersion, ignoreVersion);
            if (ret.NewerPublished)
            {
                return ret;
            }

            var patterns = new List<string>
            {
                @".?1remote-([\d|\.]*.*)-net",
                @".?latest\sversion:\s*([\d|.]*)",
            };
            foreach (var pattern in patterns)
            {
                var mc = Regex.Matches(html, pattern, RegexOptions.IgnoreCase);
                if (mc.Count <= 0)
                {
                    continue;
                }

                var versionString = mc[0].Groups[1].Value;
                var releasedVersion = VersionHelper.Version.FromString(versionString);
                if (ignoreVersion is not null && releasedVersion <= ignoreVersion)
                {
                    return VersionHelper.CheckUpdateResult.False();
                }

                if (releasedVersion > currentVersion)
                {
                    return new VersionHelper.CheckUpdateResult(
                        true,
                        versionString,
                        publishUrl,
                        versionString.FirstOrDefault() == '!' || versionString.LastOrDefault() == '!');
                }
            }

            return VersionHelper.CheckUpdateResult.False();
        }

        private void OnNewVersionRelease(VersionHelper.CheckUpdateResult result)
        {
            Execute.OnUIThread(() =>
            {
                NewVersion = result.NewerVersion;
                NewVersionUrl = result.NewerUrl;
                IsBreakingNewVersion = result.NewerHasBreakChange;

                var breakingChangeAlertVersion = IoC.Get<ConfigurationService>().Engagement.BreakingChangeAlertVersion;
                if (IsBreakingNewVersion
                    && VersionHelper.Version.FromString(result.NewerVersion) > breakingChangeAlertVersion)
                {
                    IoC.Get<IWindowManager>().ShowDialog(IoC.Get<BreakingChangeUpdateViewModel>());
                }
            });
        }
    }
}
