using Android.OS;
using Android.Views;
using Android.Widget;
using AndroidX.Fragment.App;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NetPulseAndroid
{
    public class SettingsFragment : Fragment
    {
        private Spinner _spinnerCountry;
        private Spinner _spinnerProtocol;
        private Button _btnRefresh;

        public override View OnCreateView(LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate(Resource.Layout.fragment_settings, container, false);
            _spinnerCountry = view.FindViewById<Spinner>(Resource.Id.spinnerCountry);
            _spinnerProtocol = view.FindViewById<Spinner>(Resource.Id.spinnerProtocol);
            _btnRefresh = view.FindViewById<Button>(Resource.Id.btnRefresh);

            var countries = new[] { "Любая", "DE", "US", "FR", "NL", "GB", "RU" };
            var adapter = new ArrayAdapter<string>(Activity,
                Android.Resource.Layout.SimpleSpinnerItem, countries);
            _spinnerCountry.Adapter = adapter;

            var protocols = new[] { "socks5", "http", "https" };
            _spinnerProtocol.Adapter = new ArrayAdapter<string>(Activity,
                Android.Resource.Layout.SimpleSpinnerItem, protocols);

            _btnRefresh.Click += async (s, e) =>
            {
                await LoadProxies();
            };

            return view;
        }

        private async Task LoadProxies()
        {
            string country = _spinnerCountry.SelectedItem.ToString();
            if (country == "Любая") country = null;
            string proto = _spinnerProtocol.SelectedItem.ToString();

            var list = await ProxyManager.FetchBestProxiesAsync(country, proto, 500);
            // Сохраните list в общий репозиторий (например, синглтон) для ConnectFragment
            ProxyRepository.Instance.Proxies = list;
            Toast.MakeText(Activity, $"Загружено {list.Count} прокси", ToastLength.Short).Show();
        }
    }

    public static class ProxyRepository
    {
        public static ProxyRepository Instance { get; } = new ProxyRepository();
        public List<ProxyItem> Proxies { get; set; } = new List<ProxyItem>();
    }
}
