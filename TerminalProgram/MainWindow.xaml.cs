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
using SystemOfSaving;
using Communication;

namespace TerminalProgram
{
    public enum TypeOfMessage
    {
        Char,
        String
    };

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

    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Connection Device = new Connection();

        private readonly SettingsMediator SettingsManager = new SettingsMediator();
        private DeviceData Settings = new DeviceData();

        private string[] PresetFileNames;

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

        private TypeOfMessage MessageType;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void SourceWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                Check.FilesDirectory();

                SystemOfPresets.FindFilesOfPresets(ref PresetFileNames);

                for (int i = 0; i < PresetFileNames.Length; i++)
                {
                    ComboBox_SelectedPreset.Items.Add(PresetFileNames[i]);
                }

                if (Check.SettingsFile(ref PresetFileNames, SettingsDocument) == false)
                {
                    MessageBox.Show("Файл настроек не существует в папке " + UsedDirectories.GetPath(ProgramDirectory.Settings) +
                        "\n\nНажмите ОК и выберите один из доступных пресетов в появившемся окне.",
                        "Ошибка", MessageBoxButton.OK,
                        MessageBoxImage.Error, MessageBoxResult.OK);

                    Select window = new Select(ref PresetFileNames, "Выберите пресет");
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

                RadioButton_Char.IsChecked = true;

                UpdateDeviceData(SettingsDocument);
            }

            catch (Exception error)
            {
                MessageBox.Show("Ошибка инициализации: \n\n" + error.Message + "\n\nПриложение будет закрыто.", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);

                Application.Current.Shutdown();
            }
        }
        
        private void UpdateDeviceData(string DocumentName)
        {
            try
            {
                SettingsManager.LoadSettingsFrom(UsedDirectories.GetPath(ProgramDirectory.Settings) + DocumentName + ".xml");

                List<string> Devices = SettingsManager.GetAllDevicesNames();

                if (Devices.Count > 0)
                {
                    Settings = SettingsManager.GetDeviceData(Devices[0]);

                    ComboBox_SelectedPreset.SelectedIndex = ComboBox_SelectedPreset.Items.IndexOf(DocumentName);
                }

                else
                {
                    MessageBox.Show("В документе " + UsedDirectories.GetPath(ProgramDirectory.Settings) + DocumentName +
                        ".xml" + " нет настроек устройства. Создайте их в меню Настройки.", "Предупреждение", MessageBoxButton.OK,
                        MessageBoxImage.Error, MessageBoxResult.OK);

                    ComboBox_SelectedPreset.SelectedIndex = -1;
                }
            }
            catch (Exception error)
            {
                MessageBox.Show("Ошибка чтения данных из документа. Проверьте его целостность или выберите другой файл настроек.\n\n" + error.Message, "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);

                return;
            }
        }

        private void SetUI_Connected()
        {
            Button_Connect.IsEnabled = false;
            Button_Disconnect.IsEnabled = true;

            TextBox_TX.IsEnabled = true;

            CheckBox_CRCF.IsEnabled = true;
            RadioButton_Char.IsEnabled = true;
            RadioButton_String.IsEnabled = true;

            if (RadioButton_String.IsChecked == true)
            {
                Button_Send.IsEnabled = true;
            }
            
            else
            {
                Button_Send.IsEnabled = false;
            }

            TextBox_TX.Focus();
        }

        private void SetUI_Disconnected()
        {
            Button_Connect.IsEnabled = true;
            Button_Disconnect.IsEnabled = false;

            TextBox_TX.IsEnabled = false;

            CheckBox_CRCF.IsEnabled = false;
            RadioButton_Char.IsEnabled = false;
            RadioButton_String.IsEnabled = false;
            Button_Send.IsEnabled = false;
        }

        private void MenuSettings_Click(object sender, RoutedEventArgs e)
        {
            if (PresetFileNames == null || PresetFileNames.Length == 0)
            {
                MessageBox.Show("Не найдено ни одно файла настроек.", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);
                return;
            }

            SettingsWindow Window = new SettingsWindow(UsedDirectories.GetPath(ProgramDirectory.Settings), ref PresetFileNames)
            {
                Owner = this
            };

            Window.ShowDialog();
        }

        private void MenuPreset_Save_Click(object sender, RoutedEventArgs e)
        {
            
        }

        private void MenuPreset_LoadMenuHelp_Click(object sender, RoutedEventArgs e)
        {

        }

        private void MenuHelp_Click(object sender, RoutedEventArgs e)
        {

        }

        private void ComboBox_SelectedPreset_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ComboBox_SelectedPreset.SelectedItem != null)
            {
                UpdateDeviceData(ComboBox_SelectedPreset.SelectedItem.ToString());
            }
        }

        private void Button_Connect_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                switch (Settings.TypeOfConnection)
                {
                    case "SerialPort":
                        Device.Connect(Settings.COMPort,
                                Convert.ToInt32(Settings.BaudRate),
                                Settings.Parity,
                                Convert.ToInt32(Settings.DataBits),
                                Settings.StopBits);
                        break;

                    case "Ethernet":
                        Device.Connect(Settings.IP, 
                            Convert.ToInt32(Settings.Port));
                        break;

                    default:
                        throw new Exception("В файле настроек задан неизвестный интерфейс связи.");
                }

                Device.AsyncDataReceived += Device_AsyncDataReceived;

                SetUI_Connected();
            }
            
            catch(Exception error)
            {
                MessageBox.Show("Возникла ошибка при подключении к устройству:\n\n" + error.Message, "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK,
                    MessageBoxOptions.ServiceNotification);
            }
        }

        private void Device_AsyncDataReceived(object sender, DataFromDevice e)
        {
            try
            {
                TextBlock_RX.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal,
                    new Action(delegate
                    {
                        TextBlock_RX.Text += e.RX;
                        ScrollViewer_RX.ScrollToEnd();
                    }));
            }

            catch (Exception error)
            {
                MessageBox.Show("Возникла ошибка при приеме данных от устройства:\n" + error.Message, "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK,
                    MessageBoxOptions.ServiceNotification);
            }
        }

        private void Button_Disconnect_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Device.Disconnect();

                SetUI_Disconnected();
            }

            catch(Exception error)
            {
                MessageBox.Show("Возникла ошибка при попытке отключения от устройства:\n" + error.Message, "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK,
                    MessageBoxOptions.ServiceNotification);
            }
        }

        private void TextBox_TX_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (TextBox_TX.Text != String.Empty && MessageType == TypeOfMessage.Char)
                {
                    Device.Send(TextBox_TX.Text.Substring(TextBox_TX.Text.Length - 1));
                }
            }

            catch (Exception error)
            {
                MessageBox.Show("Возникла ошибка при отправлении данных устройству:\n" + error.Message, "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);
            }
        }

        private void CheckBox_CRCF_Click(object sender, RoutedEventArgs e)
        {
            TextBox_TX.Focus();
        }

        private void RadioButton_Char_Checked(object sender, RoutedEventArgs e)
        {
            Button_Send.IsEnabled = false;

            MessageType = TypeOfMessage.Char; 
            TextBox_TX.Text = String.Empty;
            
            TextBox_TX.Focus();
        }

        private void RadioButton_String_Checked(object sender, RoutedEventArgs e)
        {
            Button_Send.IsEnabled = true;

            MessageType = TypeOfMessage.String;
            TextBox_TX.Text = String.Empty;

            TextBox_TX.Focus();
        }

        private void Button_Send_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (MessageType == TypeOfMessage.Char)
                {
                    return;
                }

                if (TextBox_TX.Text == String.Empty)
                {
                    MessageBox.Show("Буфер для отправления пуст. Введите в поле TX отправляемое значение.", "Предупреждение",
                        MessageBoxButton.OK, MessageBoxImage.Warning, MessageBoxResult.OK);

                    return;
                }

                if (CheckBox_CRCF.IsChecked == true)
                {
                    Device.Send(TextBox_TX.Text + "\r\n");
                }

                else
                {
                    Device.Send(TextBox_TX.Text);
                }
            }

            catch (Exception error)
            {
                MessageBox.Show("Возникла ошибка при отправлении данных устройству:\n" + error.Message, "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);
            }
        }

        private void SourceWindow_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Enter:
                    Button_Send_Click(Button_Send, new RoutedEventArgs());
                    break;
            }
        }

        private void Button_ClearFieldRX_Click(object sender, RoutedEventArgs e)
        {
            TextBlock_RX.Text = String.Empty;

            TextBox_TX.Focus();
        }
    }
}
