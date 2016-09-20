using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
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
using ViDi2.Training;
using ViDi2.Training.UI;
using ViDi2.Camera;

namespace ViDi2.Training.UI
{
    public class ObjectToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return ((value) != null) ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class ListToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return ((value as IList<ICamera>) != null && (value as IList<ICamera>).Count > 0) ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class ListToVisibilityConverterInv : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return ((value as IList<ICamera>) != null && (value as IList<ICamera>).Count > 0) ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public static class CameraPanelCommands
    {
        public static readonly RoutedUICommand StartGrabContinuous = new RoutedUICommand
                        (
                                "Start Grabbing continuously", "Start camera", typeof(CameraPanelCommands)
                        );

        public static readonly RoutedUICommand StopGrabContinuous = new RoutedUICommand
                        (
                                "Stop Grabbing continuously", "Stop camera", typeof(CameraPanelCommands)
                        );

        public static readonly RoutedUICommand GrabSingle = new RoutedUICommand
                        (
                                "Grab Single Frame", "Grab single", typeof(CameraPanelCommands)
                        );

        public static readonly RoutedUICommand AddImageToDatabase = new RoutedUICommand
                        (
                                "AddImageToDatabase", "AddImageToDatabase", typeof(CameraPanelCommands)
                        ); 

        public static readonly RoutedUICommand Discover = new RoutedUICommand
                        (
                                "Discover", "Discover", typeof(CameraPanelCommands)
                        );

        public static readonly RoutedUICommand SaveParametersToFile = new RoutedUICommand
                        (
                                "Save to File", "Save Parameters To File", typeof(CameraPanelCommands)
                        );

        public static readonly RoutedUICommand LoadParametersFromFile = new RoutedUICommand
                        (
                                "Load from File", "Load Parameters From File", typeof(CameraPanelCommands)
                        );

        public static readonly RoutedUICommand SaveParametersToDevice = new RoutedUICommand
                        (
                                "Save to Device", "Save Parameters To Device", typeof(CameraPanelCommands)
                        );
    }

    public class CameraEntry
    {
        public string Provider { get; set; }
        public ICamera Camera { get; set; }
    }

    public class ValueItem<T>
    {
        public ICameraParameter Parameter { get; internal set; }
        public T Value
        {
            get { return (T)Parameter.Value; }
            set { Parameter.Value = value; }
        }
        public bool IsReadOnly { get { return Parameter.IsReadOnly; } }
        public bool IsEnabled { get { return !Parameter.IsReadOnly; } }
    }

    public class DoubleValueItem : ValueItem<double> { }
    public class BoolValueItem : ValueItem<bool> { }
    public class IntValueItem : ValueItem<int> { }
    public class SizeValueItem : ValueItem<Size> { }
    public class PointValueItem : ValueItem<Point> { }
    public class StringValueItem : ValueItem<String> { }

    public class SelectionValueItem : ValueItem<object>
    {
        public ReadOnlyCollection<object> Values { get { return Parameter.Values; } }
    }

    public class ParameterItem
    {
        ICameraParameter parameter;
        public ParameterItem(ICameraParameter param)
        {
            this.parameter = param;
            var type = parameter.Value.GetType();
            if (parameter.Values.Count > 0)
                Value = new SelectionValueItem { Parameter = parameter };
            else if (type == typeof(double))
                Value = new DoubleValueItem { Parameter = parameter };
            else if (type == typeof(bool))
                Value = new BoolValueItem { Parameter = parameter };
            else if (type == typeof(int))
                Value = new IntValueItem { Parameter = parameter };
            else if (type == typeof(Size))
                Value = new SizeValueItem { Parameter = parameter };
            else if (type == typeof(Point))
                Value = new PointValueItem { Parameter = parameter };
            else if (type == typeof(String))
                Value = new StringValueItem { Parameter = parameter };
        }

        public string Name { get { return parameter.Name; } }
        public object Value { get; private set; }
    }

    /// <summary>
    /// Interaction logic for CameraControl.xaml
    /// </summary>
    public partial class CameraControl : UserControl, INotifyPropertyChanged
    {

        IStream stream;
        IImage currentImage;

        ICamera camera;
        public ICamera Camera
        {
            get { return camera; }
            set
            {
                if (camera != null)
                {
                    camera.Close();
                    camera.ImageGrabbed -= GrabbedCallback;
                }

                camera = value;

                if (camera == null) return;

                if (!camera.IsOpen)
                    camera.Open();

                camera.ImageGrabbed += GrabbedCallback;

                RaisePropertyChanged("Camera");
                RaisePropertyChanged("Parameters");
            }
        }
        SaveCommandBinding AddToDatabaseBinding;
        public CameraControl()
        {
            InitializeComponent();

            DataContext = this;

            CommandBindings.Add(new SaveCommandBinding(CameraPanelCommands.StartGrabContinuous,
               (o, e) => Camera.StartGrabContinuous(),
               (o, e) => e.CanExecute = Camera != null && !Camera.IsGrabbingContinuous));

            CommandBindings.Add(new SaveCommandBinding(CameraPanelCommands.StopGrabContinuous,
                (o, e) => Camera.StopGrabContinuous(),
                (o, e) => e.CanExecute = Camera != null && Camera.IsGrabbingContinuous));

            CommandBindings.Add(new SaveCommandBinding(CameraPanelCommands.GrabSingle,
                (o, e) => GrabbedCallback(Camera,Camera.GrabSingle()),
                (o, e) => e.CanExecute = Camera != null && !Camera.IsGrabbingContinuous));

             AddToDatabaseBinding = new SaveCommandBinding(CameraPanelCommands.AddImageToDatabase,
                (o, e) =>
                {
                    AddCurrentImageToDatabase();
                },
                (o, e) =>
                {
                    e.CanExecute = currentImage != null && Stream != null && Stream.Tools.Count > 0;
                });

            CommandBindings.Add(AddToDatabaseBinding);

            CommandBindings.Add(new SaveCommandBinding(CameraPanelCommands.Discover,
                (o, e) =>
                {
                    DiscoverCameras();
                },
                (o, e) => e.CanExecute = Camera == null || !Camera.IsGrabbingContinuous));

            CommandBindings.Add(new SaveCommandBinding(CameraPanelCommands.SaveParametersToFile,
                (o, e) =>
                {
                    var saveFileDialog = new SaveFileDialog
                    {
                        Filter = "Configuration File|*.ini|All files|*.*"
                    };

                    if (saveFileDialog.ShowDialog() == true)
                        Camera.SaveParameters(saveFileDialog.FileName);
                },
                (o, e) => e.CanExecute = Camera != null));

            CommandBindings.Add(new SaveCommandBinding(CameraPanelCommands.SaveParametersToDevice,
                (o, e) => Camera.SaveParametersToDevice(),
                (o, e) => e.CanExecute = Camera != null));

            CommandBindings.Add(new SaveCommandBinding(CameraPanelCommands.LoadParametersFromFile,
                (o, e) =>
                {
                    var openFileDialog = new OpenFileDialog
                    {
                        Filter = "Configuration File|*.ini|All files|*.*"
                    };
                    if (openFileDialog.ShowDialog() == true)
                    {
                        Camera.LoadParameters(openFileDialog.FileName);
                        RaisePropertyChanged("Parameters");
                    }
                },
                (o, e) => e.CanExecute = Camera != null && !Camera.IsGrabbingContinuous));

        }

        public List<ParameterItem> Parameters
        {
            get
            {
                return Camera == null ? null : Camera.Parameters.Select(p => new ParameterItem(p)).ToList();
            }
        }

        public List<CameraEntry> Cameras { get; private set; }

        private List<ICameraProvider> providers = new List<ICameraProvider>();

        public List<ICameraProvider> Providers
        {
            get
            {
                return providers;
            }
            set
            {
                providers = value;
            }
        }

        public IStream Stream
        {
            get
            { 
                return stream; 
            }
            set 
            { 
                stream = value;
                RaisePropertyChanged("Stream");
            }
        }

        public void DiscoverCameras()
        {
            if (Camera != null && Camera.IsGrabbingContinuous)
                Camera.StopGrabContinuous();

            var tempList = new List<CameraEntry>();

            foreach (var provider in Providers)
            {
                var list = (provider as ICameraProvider).Discover();
                tempList.AddRange(list.Select(c => new CameraEntry
                {
                    Camera = c,
                    Provider = provider.Name
                }));
            }

            Cameras = tempList;

            RaisePropertyChanged("Cameras");
        }

        List<ICamera> cameras = new List<ICamera>();

        private void RaisePropertyChanged(string prop)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(prop));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        readonly object currentImageMutex = new object();

        public BitmapSource CurrentImageSource
        {
            get
            {
                lock (currentImageMutex)
                {
                    return currentImage != null ? currentImage.BitmapSource : null;
                }
            }
        }


        public void AddCurrentImageToDatabase()
        {
            if (currentImage != null && Stream != null && Stream.Tools.Count > 0)
            {
                lock (currentImageMutex)
                {
                    if (Stream != null && Stream.Tools.Count > 0)
                        stream.AddImage(currentImage, "img-00000.png");
                }
            }
         }

        public IImage CurrentImage
        {
            /*get
           {
                return currentImage;
            }
             * */
            set
            {
                lock (currentImageMutex)
                {
                    currentImage = value;
                }

                RaisePropertyChanged("CurrentImageSource");
                RaisePropertyChanged("CurrentImage");
            }
        }



        public Action<IImage> Grabbed { get; set; } // <= to event

        private void GrabbedCallback(ICamera sender, IImage img)
        {
            currentImage = img;

            if(Grabbed != null)
               Grabbed(img);

        }



    }
}
