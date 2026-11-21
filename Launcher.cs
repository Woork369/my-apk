using Android.App;
using Android.OS;
using Android.Widget;
using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace MyAndroidApp
{
    [Activity(Label = "MyApp", MainLauncher = true)]
    public class AndroidLauncher : Activity
    {
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            var scrollView = new ScrollView(this);
            var textView = new TextView(this) { TextSize = 18 };
            textView.SetPadding(30, 30, 30, 30);
            scrollView.AddView(textView);
            SetContentView(scrollView);

            var writer = new StringWriter();
            Console.SetOut(writer);

            try
            {
                var mainMethod = Assembly.GetExecutingAssembly()
                    .GetTypes()
                    .SelectMany(t => t.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                    .FirstOrDefault(m => m.Name == "Main");

                if (mainMethod != null)
                {
                    var parameters = mainMethod.GetParameters().Length > 0 ? new object[] { Array.Empty<string>() } : null;
                    mainMethod.Invoke(null, parameters);
                }
                else
                {
                    Console.WriteLine("Не удалось найти точку входа Main.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ошибка запуска: " + (ex.InnerException?.Message ?? ex.Message));
            }

            textView.Text = writer.ToString();
        }
    }
}
