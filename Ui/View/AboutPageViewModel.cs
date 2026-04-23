using System.ComponentModel;
using _1RM.Service;
using _1RM.View.Utils;
using _1RM.View.Utils.MaskAndPop;
using Shawn.Utils.Wpf;
using Shawn.Utils.Wpf.Controls;
using Stylet;

namespace _1RM.View
{
    public class AboutPageViewModel : PopupBase
    {
        private AppUpdateService UpdateService => IoC.Get<AppUpdateService>();

        public AboutPageViewModel()
        {
            UpdateService.PropertyChanged += OnUpdateServicePropertyChanged;
        }

        public void StartVersionCheckTimer()
        {
            UpdateService.StartVersionCheckTimer();
        }

        public string CurrentVersion => UpdateService.CurrentVersion;
        public string CurrentVersionDate => UpdateService.CurrentVersionDate;
        public string NewVersion => UpdateService.NewVersion;
        public string NewVersionUrl => UpdateService.NewVersionUrl;
        public bool IsBreakingNewVersion => UpdateService.IsBreakingNewVersion;

        public void CheckUpdateAsync()
        {
            UpdateService.CheckUpdateAsync();
        }

        private void OnUpdateServicePropertyChanged(object? sender, PropertyChangedEventArgs args)
        {
            switch (args.PropertyName)
            {
                case nameof(AppUpdateService.NewVersion):
                    RaisePropertyChanged(nameof(NewVersion));
                    break;
                case nameof(AppUpdateService.NewVersionUrl):
                    RaisePropertyChanged(nameof(NewVersionUrl));
                    break;
                case nameof(AppUpdateService.IsBreakingNewVersion):
                    RaisePropertyChanged(nameof(IsBreakingNewVersion));
                    break;
            }
        }

        private RelayCommand? _cmdClose;
        public RelayCommand CmdClose
        {
            get
            {
                return _cmdClose ??= new RelayCommand((o) =>
                {
                    RequestClose();
                });
            }
        }

        private RelayCommand? _cmdUpdate;
        public RelayCommand CmdUpdate
        {
            get
            {
                return _cmdUpdate ??= new RelayCommand((o) =>
                {
                    if (IsBreakingNewVersion)
                    {
                        MaskLayerController.ShowProcessingRing();
                        IoC.Get<IWindowManager>().ShowDialog(IoC.Get<BreakingChangeUpdateViewModel>(), ownerViewModel: IoC.Get<MainWindowViewModel>());
                        MaskLayerController.HideMask();
                    }
                    else
                    {
#if FOR_MICROSOFT_STORE_ONLY
                        HyperlinkHelper.OpenUriBySystem("ms-windows-store://review/?productid=9PNMNF92JNFP");
#else
                        HyperlinkHelper.OpenUriBySystem(NewVersionUrl);
#endif
                    }
                });
            }
        }
    }
}
