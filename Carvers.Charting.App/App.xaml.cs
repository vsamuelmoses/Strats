using System.Windows;
using SciChart.Charting.Visuals;

namespace Carvers.Charting.App
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            SciChartSurface.SetRuntimeLicenseKey(@"<LicenseContract>
                  <Customer>Taniwha Enterprises, LLC</Customer>
                  <OrderId>ABT171027-6149-56145</OrderId>
                  <LicenseCount>1</LicenseCount>
                  <IsTrialLicense>false</IsTrialLicense>
                  <SupportExpires>01/25/2018 00:00:00</SupportExpires>
                  <ProductCode>SC-WPF-2D-PRO</ProductCode>
                  <KeyCode>lwAAAAEAAACG8PJB3VTUAXgAQ3VzdG9tZXI9VGFuaXdoYSBFbnRlcnByaXNlcywgTExDO09yZGVySWQ9QUJUMTcxMDI3LTYxNDktNTYxNDU7U3Vic2NyaXB0aW9uVmFsaWRUbz0yNS1KYW4tMjAxODtQcm9kdWN0Q29kZT1TQy1XUEYtMkQtUFJPDf0wGCcVIuqhXXuOSp7892XAMM+hoE0zrG0fvP/4gNHs93yUBJzYQ+4jW43VvHdI</KeyCode>
                </LicenseContract>");
        }
    }
}
