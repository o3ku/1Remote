using System;
using System.Windows;
using System.Windows.Forms;
using _1RM.Model.Protocol;
using _1RM.Utils;
using RoyalApps.Community.FreeRdp.WinForms;
using Shawn.Utils;
using Shawn.Utils.Wpf;
using Shawn.Utils.WpfResources.Theme.Styles;
using Stylet;

namespace _1RM.View.Host.ProtocolHosts
{
    public partial class FreeRdpHost : HostBase, IDisposable
    {
        private FreeRdpControl? _rdpControl;
        private readonly RDP _rdpSettings;
        private DateTime _lastLoginTime = DateTime.MinValue;

        private void BtnCancel_OnClick(object sender, RoutedEventArgs e)
        {
            this.Dispose();
            base.OnClosed?.Invoke(base.ConnectionId);
        }

        private void BtnReconn_OnClick(object sender, RoutedEventArgs e)
        {
            ReConn();
        }

        public static FreeRdpHost Create(RDP rdp, int width = 0, int height = 0)
        {
            FreeRdpHost? view = null;
            Execute.OnUIThreadSync(() =>
            {
                view = new FreeRdpHost(rdp, width, height);
            });
            return view!;
        }

        private FreeRdpHost(RDP rdp, int width = 0, int height = 0) : base(rdp, true)
        {
            InitializeComponent();
            _rdpSettings = rdp;

            GridMessageBox.Visibility = Visibility.Collapsed;
            GridLoading.Visibility = Visibility.Visible;

            InitRdpControl();
        }

        private void InitRdpControl()
        {
            _rdpControl = new FreeRdpControl
            {
                Dock = DockStyle.Fill
            };
            RdpHost.Child = _rdpControl;

            _rdpControl.Connected += (s, e) =>
            {
                SimpleLogHelper.Debug("FreeRDP Host: Connected");
                _lastLoginTime = DateTime.Now;
                Status = ProtocolHostStatus.Connected;
                Execute.OnUIThread(() =>
                {
                    RdpHost.Visibility = Visibility.Visible;
                    GridLoading.Visibility = Visibility.Collapsed;
                    GridMessageBox.Visibility = Visibility.Collapsed;
                });
            };

            _rdpControl.CertificateError += (s, e) =>
            {
                SimpleLogHelper.Warning("FreeRDP Host: Certificate validation failed, continue with ignored certificate");
                e.Continue();
            };

            _rdpControl.VerifyCredentials += (s, e) =>
            {
                var password = UnSafeStringEncipher.DecryptOrReturnOriginalString(_rdpSettings.Password);
                SimpleLogHelper.Warning("FreeRDP Host: VerifyCredentials requested, retry with configured credentials");
                e.SetCredentials(_rdpSettings.UserName, _rdpSettings.Domain, password);
            };

            _rdpControl.Disconnected += (s, e) =>
            {
                var errorMessage = string.IsNullOrWhiteSpace(e.ErrorMessage) ? "Connection closed" : e.ErrorMessage;
                SimpleLogHelper.Warning($"FreeRDP Host: Disconnected (ExitCode: {e.ExitCode}, UserInitiated: {e.UserInitiated}, Error: {errorMessage})");
                Status = ProtocolHostStatus.Disconnected;
                Execute.OnUIThread(() =>
                {
                    RdpHost.Visibility = Visibility.Collapsed;
                    GridLoading.Visibility = Visibility.Collapsed;
                    GridMessageBox.Visibility = Visibility.Visible;
                    TbMessageTitle.Text = e.UserInitiated ? "Disconnected" : "Connection Failed";
                    TbMessage.Text = errorMessage;
                });
            };
        }

        public override void Conn()
        {
            if (_rdpControl == null) return;

            try
            {
                Status = ProtocolHostStatus.Connecting;
                GridLoading.Visibility = Visibility.Visible;
                RdpHost.Visibility = Visibility.Collapsed;

                var config = _rdpControl.Configuration;
                config.Server = _rdpSettings.Address;
                config.Username = _rdpSettings.UserName;
                config.Domain = _rdpSettings.Domain;
                config.Port = _rdpSettings.GetPort();
                config.LoadBalanceInfo = _rdpSettings.LoadBalanceInfo;

                var password = UnSafeStringEncipher.DecryptOrReturnOriginalString(_rdpSettings.Password);
                if (!string.IsNullOrEmpty(password))
                {
                    config.Password = password;
                }

                config.DesktopWidth = 0;
                config.DesktopHeight = 0;
                config.AutoScaling = true;

                _rdpControl.Connect();
            }
            catch (Exception ex)
            {
                SimpleLogHelper.Error($"FreeRDP Host: Connect failed - {ex.Message}");
                Status = ProtocolHostStatus.Disconnected;
                TbMessageTitle.Text = "Connection Error";
                TbMessage.Text = ex.Message;
                GridMessageBox.Visibility = Visibility.Visible;
                GridLoading.Visibility = Visibility.Collapsed;
            }
        }

        public override void Close()
        {
            this.Dispose();
            base.Close();
        }

        public new void Disconnect()
        {
            try
            {
                _rdpControl?.Disconnect();
            }
            catch (Exception ex)
            {
                SimpleLogHelper.Warning($"FreeRDP Host: Disconnect error - {ex.Message}");
            }
            Status = ProtocolHostStatus.Disconnected;
        }

        public override void ReConn()
        {
            Disconnect();
            Execute.OnUIThread(() =>
            {
                GridLoading.Visibility = Visibility.Visible;
                GridMessageBox.Visibility = Visibility.Collapsed;
                RdpHost.Visibility = Visibility.Collapsed;
            });
            Conn();
        }

        public override ProtocolHostType GetProtocolHostType() => ProtocolHostType.Native;

        public override IntPtr GetHostHwnd()
        {
            if (_rdpControl == null) return IntPtr.Zero;
            return _rdpControl.Handle;
        }

        public override void SetParentWindow(WindowBase? value)
        {
            base.SetParentWindow(value);
            if (value is FullScreenWindowView && value.IsLoaded && value.IsClosed == false)
            {
                GoFullScreen();
            }
        }

        protected override void GoFullScreen()
        {
            if (_rdpSettings.RdpFullScreenFlag == ERdpFullScreenFlag.Disable
                || ParentWindow is not FullScreenWindowView)
            {
                return;
            }
        }

        public override void FocusOnMe()
        {
            Execute.OnUIThread(() =>
            {
                _rdpControl?.Focus();
            });
        }

        public void Dispose()
        {
            try
            {
                _rdpControl?.Disconnect();
                _rdpControl?.Dispose();
            }
            catch (Exception ex)
            {
                SimpleLogHelper.Warning($"FreeRDP Host: Dispose error - {ex.Message}");
            }
            _rdpControl = null;
            SimpleLogHelper.Debug($"FreeRDP Host: Disposed {this.GetHashCode()}");
        }
    }
}
