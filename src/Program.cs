using System;
using System.Windows.Forms;
using RA2LiveController.UI;

namespace RA2LiveController
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());
        }
    }
}
