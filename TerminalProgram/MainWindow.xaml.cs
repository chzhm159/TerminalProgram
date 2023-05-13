﻿using System;
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
using System.IO;
using System.IO.Ports;
using TerminalProgram.ServiceWindows;
using TerminalProgram.Settings;
using TerminalProgram.Protocols;
using TerminalProgram.Protocols.NoProtocol;
using TerminalProgram.Protocols.Modbus;
using TerminalProgram.Protocols.Http;

namespace TerminalProgram
{
    public enum ProgramDirectory
    {
        Settings
    }

    public static class UsedDirectories
    {
        private readonly static string Path_Settings = "Settings/";

        public static string GetPath(ProgramDirectory Type)
        {
            switch (Type)
            {
                case ProgramDirectory.Settings:
                    return Path_Settings;

                default:
                    throw new Exception("Выбрана неизвестный тип директории.");
            }
        }
    }

    public class ConnectArgs : EventArgs
    {
        public IConnection ConnectedDevice;

        public ConnectArgs(IConnection ConnectedDevice)
        {
            this.ConnectedDevice = ConnectedDevice;
        }
    }

    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public event EventHandler<ConnectArgs>? DeviceIsConnect;
        public event EventHandler<ConnectArgs>? DeviceIsDisconnected;

        public DeviceData Settings { get; private set; } = new DeviceData();

        // Значение кодировки по умолчанию
        public static Encoding GlobalEncoding { get; private set; } = Encoding.Default;

        public ProtocolMode? SelectedProtocol { get; private set; }

        private IConnection? Client = null;
        
        private string[]? PresetFileNames;

        private UI_NoProtocol? NoProtocolPage = null;
        private UI_Modbus? ModbusPage = null;
        private UI_Http? HttpPage = null;

        private string SettingsDocument
        {
            get
            {
                Properties.Settings.Default.Reload();
                return Properties.Settings.Default.SettingsDocument;
            }

            set
            {
                Properties.Settings.Default.SettingsDocument = value;
                Properties.Settings.Default.Save();
            }
        }

        public MainWindow()
        {
            InitializeComponent();
        }

        public static string[] GetDeviceList()
        {
            string[] Devices = Directory.GetFiles(UsedDirectories.GetPath(ProgramDirectory.Settings));

            for (int i = 0; i < Devices.Length; i++)
            {
                Devices[i] = System.IO.Path.GetFileNameWithoutExtension(Devices[i]);
            }

            return Devices;
        }

        private async void SourceWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                if (Directory.Exists(UsedDirectories.GetPath(ProgramDirectory.Settings)) == false)
                {
                    Directory.CreateDirectory(UsedDirectories.GetPath(ProgramDirectory.Settings));
                }

                PresetFileNames = await SystemOfSettings.FindFilesOfPresets();

                foreach(string FileName in PresetFileNames)
                {
                    ComboBox_SelectedPreset.Items.Add(FileName);
                }
                
                if (PresetFileNames.Contains(SettingsDocument) == false)
                {
                    MessageBox.Show("Файл настроек не существует в папке " + UsedDirectories.GetPath(ProgramDirectory.Settings) +
                        "\n\nНажмите ОК и выберите один из доступных файлов в появившемся окне.",
                        this.Title, MessageBoxButton.OK,
                        MessageBoxImage.Warning, MessageBoxResult.OK);

                    ComboBoxWindow window = new ComboBoxWindow(ref PresetFileNames)
                    {
                        Owner = this
                    };

                    window.ShowDialog();

                    if (window.SelectedDocumentPath != String.Empty)
                    {
                        SettingsDocument = window.SelectedDocumentPath;
                    }

                    else
                    {
                        Application.Current.Shutdown();
                        return;
                    }
                }

                SetUI_Disconnected();

                SystemOfSettings.Settings_FilePath = UsedDirectories.GetPath(ProgramDirectory.Settings) +
                    SettingsDocument + SystemOfSettings.FileType;

                NoProtocolPage = new UI_NoProtocol(this);

                NoProtocolPage.ErrorHandler += CommonErrorHandler;

                ModbusPage = new UI_Modbus(this);

                ModbusPage.ErrorHandler += CommonErrorHandler;

                HttpPage = new UI_Http(this);

                RadioButton_NoProtocol.IsChecked = true;

                await UpdateDeviceData(SettingsDocument);
            }

            catch (Exception error)
            {
                MessageBox.Show("Ошибка инициализации: \n\n" + error.Message + "\n\nПриложение будет закрыто.", this.Title,
                    MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);

                Application.Current.Shutdown();
            }
        }

        private void CommonErrorHandler(object? sender, EventArgs e)
        {
            Button_Disconnect_Click(this, new RoutedEventArgs());
        }

        private async void SourceWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                if (Client != null && Client.IsConnected)
                {
                    if (MessageBox.Show("Клиент ещё подключен к хосту.\nЗакрыть программу?", this.Title,
                        MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
                    {
                        e.Cancel = true;
                        return;
                    }

                    await Client.Disconnect();
                }
            }
            
            catch(Exception error)
            {
                MessageBox.Show("Возникла ошибка во время закрытия программы.\n\n" + error.Message, this.Title,
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            this.DragMove();
        }

        private void Button_MinimizeApplication_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;

            ////// Получаем размеры рабочей области экрана, включая панель задач Windows
            //var workingArea = SystemParameters.WorkArea;
            ////Left = workingArea.Left;
            ////Top = workingArea.Top;
            ////Width = workingArea.Width;
            ////Height = workingArea.Height;

            //// Определяем монитор, на котором находится окно
            //if (Left >= SystemParameters.VirtualScreenLeft &&
            //    Left + ActualWidth <= SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth &&
            //    Top >= SystemParameters.VirtualScreenTop &&
            //    Top + ActualHeight <= SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight)
            //{
            //    // Окно находится на главном мониторе
            //    // Добавьте свой код здесь
            //}
            //else
            //{
            //    // Окно находится на второстепенном мониторе
            //    // Добавьте свой код здесь
            //}
        }

        private void Button_CloseApplication_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private async Task UpdateDeviceData(string DocumentName)
        {
            try
            {
                DeviceData Device = await SystemOfSettings.Read();

                Settings = (DeviceData)Device.Clone();

                ComboBox_SelectedPreset.SelectedIndex = ComboBox_SelectedPreset.Items.IndexOf(DocumentName);

                if (Settings.GlobalEncoding == null)
                {
                    throw new Exception("Не удалось обработать значение кодировки.");
                }

                GlobalEncoding = GetEncoding(Settings.GlobalEncoding);
            }

            catch (Exception error)
            {
                MessageBox.Show("Ошибка чтения данных из документа." +
                    " Проверьте его целостность или выберите другой файл настроек." +
                    " Возможно данный файл не совместим с текущей версией программы.\n\n" + error.Message,
                    this.Title, MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);

                return;
            }
        }

        private Encoding GetEncoding(string EncodingName)
        {
            switch (EncodingName)
            {
                case "ASCII":
                    return Encoding.ASCII;

                case "Unicode":
                    return Encoding.Unicode;

                case "UTF-32":
                    return Encoding.UTF32;

                case "UTF-8":
                    return Encoding.UTF8;

                default:
                    throw new Exception("Задан неизвестный тип кодировки: " + EncodingName);
            }
        }

        private async void MenuSettings_Click(object sender, RoutedEventArgs e)
        {
            if (PresetFileNames == null || PresetFileNames.Length == 0)
            {
                MessageBox.Show("Не найдено ни одно файла настроек.", this.Title,
                    MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);

                return;
            }

            if (SettingsDocument == String.Empty)
            {
                MessageBox.Show("Не выбран файл настроек", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);

                return;
            }

            SettingsWindow Window = new SettingsWindow(SettingsDocument)
            {
                Owner = this
            };

            Window.ShowDialog();

            string[] Devices = MainWindow.GetDeviceList();

            ComboBox_SelectedPreset.Items.Clear();

            bool SettingDocumentExists = false;

            for(int i = 0; i < Devices.Length; i++)
            {
                ComboBox_SelectedPreset.Items.Add(Devices[i]);
                
                if (Devices[i] == SettingsDocument)
                {
                    SettingDocumentExists = true;
                }
            }

            if (Window.SettingsIsChanged)
            {
                SettingsDocument = Window.SettingsDocument;
            }

            else if (SettingDocumentExists == false && Devices.Length != 0)
            {
                SettingsDocument = Devices[0];
            }

            await UpdateDeviceData(SettingsDocument);
        }

        private async void ComboBox_SelectedPreset_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (ComboBox_SelectedPreset.SelectedItem != null)
                {
                    string? DocumentName = ComboBox_SelectedPreset.SelectedItem.ToString();

                    if (DocumentName == null)
                    {
                        throw new Exception("Не удалось обработать имя выбранного пресета.");
                    }

                    SettingsDocument = DocumentName;

                    await UpdateDeviceData(SettingsDocument);
                }
            }
            
            catch (Exception error)
            {
                MessageBox.Show("Возникла ошибка при изменении пресета.\n\n" + error.Message, this.Title,
                    MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);
            }
        }

        private void Button_Connect_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (SelectedProtocol == null)
                {
                    throw new Exception("Не выбран протокол.");
                }

                switch (Settings.TypeOfConnection)
                {
                    case "SerialPort":

                        Client = new SerialPortClient();

                        Client.Connect(new ConnectionInfo(new SerialPortInfo(
                            Settings.Connection_SerialPort?.COMPort,
                            Settings.Connection_SerialPort?.BaudRate_IsCustom == "Enable" ? 
                                Settings.Connection_SerialPort?.BaudRate_Custom : Settings.Connection_SerialPort?.BaudRate,
                            Settings.Connection_SerialPort?.Parity,
                            Settings.Connection_SerialPort?.DataBits,
                            Settings.Connection_SerialPort?.StopBits
                            ),
                            GlobalEncoding));

                        break;

                    case "Ethernet":

                        Client = new IPClient();

                        Client.Connect(new ConnectionInfo(new SocketInfo(
                            Settings.Connection_IP?.IP_Address,
                            Settings.Connection_IP?.Port
                            ),
                            GlobalEncoding));

                        break;

                    default:
                        throw new Exception("В файле настроек задан неизвестный интерфейс связи.");
                }

                SetUI_Connected();

                ProtocolMode_Modbus? ModbusProtocol = SelectedProtocol as ProtocolMode_Modbus;

                ModbusProtocol?.UpdateTimeouts(Settings);

                SelectedProtocol.InitMode(Client);

                DeviceIsConnect?.Invoke(this, new ConnectArgs(Client));
            }
            
            catch(Exception error)
            {
                MessageBox.Show("Возникла ошибка при подключении к хосту.\n\n" + error.Message, this.Title,
                    MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);
            }
        }

        private async void Button_Disconnect_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (Client == null)
                {
                    return;
                }

                try
                {
                    await Client.Disconnect();
                }

                catch (Exception error)
                {
                    MessageBox.Show("Возникла ошибка при попытке отключения от устройства:\n" + error.Message, this.Title,
                        MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);
                }

                SetUI_Disconnected();

                DeviceIsDisconnected?.Invoke(this, new ConnectArgs(Client));
            }

            catch(Exception error)
            {
                MessageBox.Show("Возникла ошибка при нажатии на кнопку \"Отключить\":\n" + error.Message, this.Title,
                    MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);
            }
        }

        private void SetUI_Connected()
        {
            Button_Connect.IsEnabled = false;
            Button_Disconnect.IsEnabled = true;

            MenuSettings.IsEnabled = false;
        }

        private void SetUI_Disconnected()
        {
            Button_Connect.IsEnabled = true;
            Button_Disconnect.IsEnabled = false;

            MenuSettings.IsEnabled = true;
        }

        private void RadioButton_NoProtocol_Checked(object sender, RoutedEventArgs e)
        {
            if (NoProtocolPage == null)
            {
                return;
            }

            if (Frame_ActionUI.Navigate(NoProtocolPage) == false)
            {
                MessageBox.Show("Не удалось перейти на страницу " + NoProtocolPage.Name, this.Title,
                    MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);

                return;
            }

            GridRow_Header.Height = new GridLength(100);
            TextBlock_SelectedPreset.Visibility = Visibility.Visible;
            ComboBox_SelectedPreset.Visibility = Visibility.Visible;
            Button_Connect.Visibility = Visibility.Visible;
            Button_Disconnect.Visibility = Visibility.Visible;

            SelectedProtocol = new ProtocolMode_NoProtocol(Client);
        }

        private void RadioButton_Protocol_Modbus_Checked(object sender, RoutedEventArgs e)
        {
            if (ModbusPage == null)
            {
                return;
            }

            if (Frame_ActionUI.Navigate(ModbusPage) == false)
            {
                MessageBox.Show("Не удалось перейти на страницу " + ModbusPage.Name, this.Title,
                    MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);

                return;
            }

            GridRow_Header.Height = new GridLength(100);
            TextBlock_SelectedPreset.Visibility = Visibility.Visible;
            ComboBox_SelectedPreset.Visibility = Visibility.Visible;
            Button_Connect.Visibility = Visibility.Visible;
            Button_Disconnect.Visibility = Visibility.Visible;

            SelectedProtocol = new ProtocolMode_Modbus(Client, Settings);
        }

        private void RadioButton_Protocol_Http_Checked(object sender, RoutedEventArgs e)
        {
            if (HttpPage == null)
            {
                return;
            }

            if (Frame_ActionUI.Navigate(HttpPage) == false)
            {
                MessageBox.Show("Не удалось перейти на страницу " + HttpPage.Name, this.Title,
                    MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);

                return;
            }

            GridRow_Header.Height = new GridLength(50);
            TextBlock_SelectedPreset.Visibility = Visibility.Hidden;
            ComboBox_SelectedPreset.Visibility = Visibility.Hidden;
            Button_Connect.Visibility = Visibility.Hidden;
            Button_Disconnect.Visibility = Visibility.Hidden;
        }

        private void MenuAbout_Click(object sender, RoutedEventArgs e)
        {
            AboutWindow window = new AboutWindow()
            {
                Owner = this
            };

            window.ShowDialog();
        }
    }
}
