using Android.App;
using Android.OS;
using AndroidX.AppCompat.App;
using AndroidX.ViewPager2.Widget;
using Google.Android.Material.Tabs;
using AndroidX.Fragment.App;
using AndroidX.ViewPager2.Adapter;
using AndroidX.Lifecycle;

namespace NetPulseAndroid
{
    // ИСПРАВЛЕНИЕ: Добавлен Exported = true
    [Activity(Label = "@string/app_name", MainLauncher = true, Theme = "@style/Theme.Material3.Dark", Exported = true)]
    public class MainActivity : AppCompatActivity
    {
        private ViewPager2 _viewPager;
        private TabLayout _tabLayout;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.activity_main);

            _viewPager = FindViewById<ViewPager2>(Resource.Id.viewPager);
            _tabLayout = FindViewById<TabLayout>(Resource.Id.tabLayout);

            _viewPager.Adapter = new MainPagerAdapter(SupportFragmentManager, Lifecycle);
            
            var strategy = new TabConfigStrategy();
            new TabLayoutMediator(_tabLayout, _viewPager, strategy).Attach();
        }

        private class TabConfigStrategy : Java.Lang.Object, TabLayoutMediator.ITabConfigurationStrategy
        {
            public void OnConfigureTab(TabLayout.Tab tab, int position)
            {
                tab.SetText(position switch
                {
                    0 => "Tunnel",
                    1 => "Настройки",
                    2 => "Подключение",
                    _ => ""
                });
            }
        }

        private class MainPagerAdapter : FragmentStateAdapter
        {
            public MainPagerAdapter(AndroidX.Fragment.App.FragmentManager fm, AndroidX.Lifecycle.Lifecycle lifecycle) 
                : base(fm, lifecycle) { }

            public override int ItemCount => 3;

            public override AndroidX.Fragment.App.Fragment CreateFragment(int position) => position switch
            {
                0 => new TunnelFragment(),
                1 => new SettingsFragment(),
                2 => new ConnectFragment(),
                _ => new TunnelFragment()
            };
        }
    }
}
