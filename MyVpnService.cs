using Android.App;
using Android.Content;
using Android.Net;
using Android.OS;
using AndroidX.Core.App;
using Java.IO;
using System;
using System.Threading;

namespace NetPulseAndroid
{
    [Service(Permission = "android.permission.BIND_VPN_SERVICE", ForegroundServiceType = Android.Content.PM.ForegroundService.TypeDataSync)]
    [IntentFilter(new[] { "android.net.VpnService" })]
    public class MyVpnService : VpnService
    {
        private const string ChannelId = "vpn_channel";
        private const int NotificationId = 1001;
        private ParcelFileDescriptor _vpnInterface;
        private Thread _tunnelThread;
        private volatile bool _isRunning;

        // ИСПРАВЛЕНИЕ: Возвращаемый тип изменен с int на StartCommandResult
        public override StartCommandResult OnStartCommand(Intent intent, StartCommandFlags flags, int startId)
        {
            if (intent?.Action == "CONNECT")
            {
                string host = intent.GetStringExtra("proxy_host");
                int port = intent.GetIntExtra("proxy_port", 1080);
                string protocol = intent.GetStringExtra("proxy_protocol") ?? "socks5";

                StartForegroundWithNotification(host, port);
                StartVpn(host, port, protocol);
            }
            else if (intent?.Action == "DISCONNECT")
            {
                StopVpn();
            }
            // ИСПРАВЛЕНИЕ: StartCommandResult вместо StartCommandFlags
            return StartCommandResult.Sticky; 
        }

        private void StartForegroundWithNotification(string host, int port)
        {
            CreateNotificationChannel();

            var stopIntent = new Intent(this, typeof(MyVpnService));
            stopIntent.SetAction("DISCONNECT");
            var stopPending = PendingIntent.GetService(this, 0, stopIntent,
                PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

            var notification = new NotificationCompat.Builder(this, ChannelId)
                .SetContentTitle("NetPulse VPN активен")
                .SetContentText($"Прокси: {host}:{port}")
                .SetSmallIcon(Resource.Drawable.ic_vpn)
                .SetOngoing(true)
                .AddAction(Resource.Drawable.ic_vpn, "Отключить", stopPending)
                .SetPriority(NotificationCompat.PriorityLow)
                .Build();

            StartForeground(NotificationId, notification);
        }

        private void CreateNotificationChannel()
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {
                var channel = new NotificationChannel(ChannelId, "VPN Service",
                    NotificationImportance.Low);
                var manager = (NotificationManager)GetSystemService(NotificationService);
                manager.CreateNotificationChannel(channel);
            }
        }

        private void StartVpn(string proxyHost, int proxyPort, string protocol)
        {
            if (_isRunning) return;

            var builder = new Builder()
                .SetSession("NetPulse")
                .AddAddress("10.0.0.2", 32)
                .AddRoute("0.0.0.0", 0)
                .AddDnsServer("8.8.8.8")
                .SetMtu(1500);

            _vpnInterface = builder.Establish();
            if (_vpnInterface == null)
            {
                StopSelf();
                return;
            }

            _isRunning = true;
            _tunnelThread = new Thread(() => RunTunnel(proxyHost, proxyPort, protocol));
            _tunnelThread.Start();
        }

        private void RunTunnel(string proxyHost, int proxyPort, string protocol)
        {
            try
            {
                using (var input = new FileInputStream(_vpnInterface.FileDescriptor))
                {
                    byte[] buffer = new byte[1500];
                    while (_isRunning)
                    {
                        int len = input.Read(buffer);
                        if (len <= 0) break;
                        // Заглушка: здесь должен быть движок tun2socks
                    }
                }
            }
            catch (Exception ex)
            {
                Android.Util.Log.Error("NetPulseVPN", ex.ToString());
            }
            finally
            {
                StopVpn();
            }
        }

        private void StopVpn()
        {
            _isRunning = false;
            try { _tunnelThread?.Join(1000); } catch { }
            try { _vpnInterface?.Close(); } catch { }
            _vpnInterface = null;
            StopForeground(true);
            StopSelf();
        }

        public override void OnDestroy()
        {
            StopVpn();
            base.OnDestroy();
        }
    }
}
