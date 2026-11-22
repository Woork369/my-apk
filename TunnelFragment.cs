using Android.OS;
using Android.Views;
using Android.Widget;
using AndroidX.Fragment.App;
using Google.Android.Material.Button;

namespace NetPulseAndroid
{
    public class TunnelFragment : Fragment
    {
        private MaterialButton _btnToggle;
        private TextView _txtStatus;

        public override View OnCreateView(LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate(Resource.Layout.fragment_tunnel, container, false);
            _btnToggle = view.FindViewById<MaterialButton>(Resource.Id.btnToggle);
            _txtStatus = view.FindViewById<TextView>(Resource.Id.txtStatus);

            _btnToggle.Click += (s, e) =>
            {
                var activity = Activity;
                if (activity == null) return;

                // Проверяем разрешение VPN
                var intent = VpnService.Prepare(activity);
                if (intent != null)
                {
                    StartActivityForResult(intent, 0);
                }
                else
                {
                    ConnectVpn();
                }
            };

            return view;
        }

        private void ConnectVpn()
        {
            var svcIntent = new Android.Content.Intent(Activity, typeof(MyVpnService));
            svcIntent.SetAction("CONNECT");
            svcIntent.PutExtra("proxy_host", "127.0.0.1"); // замените на выбранный прокси
            svcIntent.PutExtra("proxy_port", 1080);
            svcIntent.PutExtra("proxy_protocol", "socks5");
            Activity.StartService(svcIntent);

            _txtStatus.Text = "Подключение...";
            _btnToggle.Text = "ОТКЛЮЧИТЬ";
        }

        public override void OnActivityResult(int requestCode, int resultCode, Android.Content.Intent data)
        {
            base.OnActivityResult(requestCode, resultCode, data);
            if (requestCode == 0 && resultCode == (int)Android.App.Result.Ok)
                ConnectVpn();
        }
    }
}
