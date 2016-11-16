using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Win32;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Globalization;
using ViDi2.Camera;
using System.Threading;
using System.Collections.ObjectModel;

namespace ViDi2.Training.UI
{
    /// <summary>
    /// Camera Panel Plugin
    /// </summary>
    public partial class CameraPanel : Window, IPlugin
    {
        public CameraPanel()
        {
            InitializeComponent();

            cameraControl.ImageGrabbed += (sender, img) => 
            {
                lock (pendingProcessImageMutex)
                {
                    pendingProcessImage = img;
                    Monitor.PulseAll(pendingProcessImageMutex);
                }
            };

            Closing += (o, a) =>
            {
                a.Cancel = true;
                Hide();
            };
        }

        string IPlugin.Name { get { return "Camera Panel"; } }
        string IPlugin.Description { get { return "Configure and control supported and available cameras"; } }
        int IPlugin.Version { get { return 1; } }

        bool initialized = false;
        void IPlugin.Initialize(IPluginContext context)
        {
            if (initialized)
                return;

            initialized = true;

            this.context = context;

            mainMenuItem = new MenuItem { Header = "Cameras" };

            MenuItem showPanelItem = new MenuItem { Header = "Camera Panel" };

            showPanelItem.Click += (o, a) =>
            {
                Show();
                Activate();
            };

            mainMenuItem.Items.Add(showPanelItem);
            context.MainWindow.MainMenu.Items.Add(mainMenuItem);

            context.MainWindow.ToolChain.StreamSelected += 
                (stream) => { cameraControl.Stream = stream; };

            providersMenuItem = new MenuItem
            {
                Header = "providers ...",
                ToolTip = "All providers (CameraManagers) currently loaded as plugin"
            };

            mainMenuItem.Items.Add(providersMenuItem);

            foreach (IPlugin plugin in context.Plugins)
            {
                if (plugin is ICameraProvider)
                {
                    plugin.Initialize(context);
                }
            }

            //mainMenuItem.Items.Add(discoverMenuItem);

            UpdateProviders();

            StartProcessTask();
        }

        private void UpdateProviders()
        {
            providersMenuItem.Items.Clear();

            foreach (var plugin in context.Plugins)
            {
                if (plugin is ICameraProvider &&
                    !cameraControl.Providers.Exists(p => p == plugin))
                {
                    cameraControl.Providers.Add(plugin as ICameraProvider);
                }
            }

            foreach (var plugin in cameraControl.Providers)
            {
                MenuItem it = new MenuItem();
                it.Header = plugin.Name;
                providersMenuItem.Items.Add(it);
            }

            MenuItem discoverMenuItem = new MenuItem();
            discoverMenuItem.Header = "Discover Camera Providers plugins ...";
            discoverMenuItem.Click += (o, e) =>
            {
                UpdateProviders();
            };

            providersMenuItem.Items.Add(discoverMenuItem);
        }

        void IPlugin.DeInitialize()
        {
            //if (Camera != null && Camera.IsLive)
            //    Camera.StopLive();

            StopProcessTask();
        }

        Task processTask;
        CancellationTokenSource cts;

        void StartProcessTask()
        {
            cts = new CancellationTokenSource();

            processTask = Task.Run(() =>
            {
                IImage processImage = null;

                while (!cts.Token.IsCancellationRequested)
                {
                    lock (pendingProcessImageMutex)
                    {
                        while (!cts.Token.IsCancellationRequested && pendingProcessImage == null)
                            Monitor.Wait(pendingProcessImageMutex);

                        processImage = pendingProcessImage;
                        pendingProcessImage = null;
                    }

                    if (context.MainWindow.IsProductionMode && 
                        context.MainWindow.ToolChain.Tool != null &&
                        processImage != null)
                    {
                        try
                        {
                            context.MainWindow.SampleViewer.Sample =
                                context.MainWindow.ToolChain.Stream.Process(processImage);
                        }
                        catch (Exception ex)
                        {
                            Dispatcher.Invoke(() => MessageBox.Show(ex.Message));
                        }
                    }
                }
            }, cts.Token);
        }

        void StopProcessTask()
        {
            cts.Cancel();

            lock (pendingProcessImageMutex)
                Monitor.PulseAll(pendingProcessImageMutex);

            processTask.Wait();

            cts.Dispose();
        }

        readonly object pendingProcessImageMutex = new object();
        IImage pendingProcessImage;

        IPluginContext context;
        MenuItem mainMenuItem;
        MenuItem providersMenuItem;
    }
}
