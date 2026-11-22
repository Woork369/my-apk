using Android.OS;
using Android.Views;
using AndroidX.Fragment.App;
using AndroidX.RecyclerView.Widget;

namespace NetPulseAndroid
{
    public class ConnectFragment : Fragment
    {
        private RecyclerView _recyclerView;
        private ProxyAdapter _adapter;

        public override View OnCreateView(LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate(Resource.Layout.fragment_connect, container, false);
            _recyclerView = view.FindViewById<RecyclerView>(Resource.Id.recyclerProxies);
            _recyclerView.SetLayoutManager(new LinearLayoutManager(Activity));
            _adapter = new ProxyAdapter(ProxyRepository.Instance.Proxies);
            _recyclerView.SetAdapter(_adapter);
            return view;
        }
    }

    public class ProxyAdapter : RecyclerView.Adapter
    {
        private readonly System.Collections.Generic.List<ProxyItem> _items;

        public ProxyAdapter(System.Collections.Generic.List<ProxyItem> items) => _items = items;

        public override int ItemCount => _items.Count;

        public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType)
        {
            var view = LayoutInflater.From(parent.Context)
                .Inflate(Android.Resource.Layout.SimpleListItem1, parent, false);
            return new ViewHolder(view);
        }

        public override void OnBindViewHolder(RecyclerView.ViewHolder holder, int position)
        {
            var vh = (ViewHolder)holder;
            var item = _items[position];
            vh.TextView.Text = item.ToString();
        }

        private class ViewHolder : RecyclerView.ViewHolder
        {
            public Android.Widget.TextView TextView { get; }
            public ViewHolder(View itemView) : base(itemView)
            {
                TextView = itemView.FindViewById<Android.Widget.TextView>(Android.Resource.Id.Text1);
            }
        }
    }
}
