﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
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

namespace TerminalProgram.Protocols.NoProtocol
{
    public enum TypeOfMessage
    {
        Char,
        String
    };

    /// <summary>
    /// Логика взаимодействия для NoProtocol.xaml
    /// </summary>
    public partial class UI_NoProtocol : Page
    {
        public event EventHandler<EventArgs> ErrorHandler;

        private IConnection Client = null;

        private TypeOfMessage MessageType;

        private readonly string MainWindowTitle;

        private object locker = new object();

        public UI_NoProtocol(MainWindow window)
        {
            InitializeComponent();

            MainWindowTitle = window.Title;
            
            window.DeviceIsConnect += MainWindow_DeviceIsConnect;
            window.DeviceIsDisconnected += MainWindow_DeviceIsDisconnected;

            SetUI_Disconnected();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            RadioButton_String.Checked -= RadioButton_String_Checked;
            RadioButton_String.IsChecked = true;
            RadioButton_String.Checked += RadioButton_String_Checked;

            MessageType = TypeOfMessage.String;
            TextBox_TX.Text = String.Empty;

            TextBox_TX.Focus();
        }

        private void Page_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Enter:
                    Button_Send_Click(Button_Send, new RoutedEventArgs());
                    break;
            }
        }

        private void MainWindow_DeviceIsConnect(object sender, ConnectArgs e)
        {
            if (e.ConnectedDevice.IsConnected)
            {
                Client = e.ConnectedDevice;

                SetUI_Connected();

                Client.DataReceived += Client_DataReceived;                
            }            
        }

        private void MainWindow_DeviceIsDisconnected(object sender, ConnectArgs e)
        {
            TextBox_TX.Text = String.Empty;

            SetUI_Disconnected();
        }

        private void SetUI_Connected()
        {
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
            TextBox_TX.IsEnabled = false;

            CheckBox_CRCF.IsEnabled = false;
            RadioButton_Char.IsEnabled = false;
            RadioButton_String.IsEnabled = false;
            Button_Send.IsEnabled = false;
        }

        private void TextBox_TX_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (TextBox_TX.Text != String.Empty && MessageType == TypeOfMessage.Char)
                {
                    SendMessage(TextBox_TX.Text.Last().ToString());
                }
            }

            catch (Exception error)
            {
                MessageBox.Show("Возникла ошибка при отправлении данных устройству:\n" + error.Message, MainWindowTitle,
                    MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);
            }
        }

        private void SendMessage(string StringMessage)
        {
            if (StringMessage == String.Empty)
            {
                MessageBox.Show("Буфер для отправления пуст. Введите в поле TX отправляемое значение.", MainWindowTitle,
                    MessageBoxButton.OK, MessageBoxImage.Warning, MessageBoxResult.OK);

                return;
            }

            byte[] Message;

            if (CheckBox_CRCF.IsChecked == true)
            {
                Message = MainWindow.GlobalEncoding.GetBytes(StringMessage + "\r\n");
            }

            else
            {
                Message = MainWindow.GlobalEncoding.GetBytes(StringMessage);
            }

            Client.Send(Message, Message.Length);
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

                SendMessage(TextBox_TX.Text);
            }

            catch (Exception error)
            {
                if (ErrorHandler != null)
                {
                    ErrorHandler(this, new EventArgs());

                    MessageBox.Show("Возникла ошибка при отправлении данных:\n" + error.Message +
                        "\n\nКлиент был отключен.", MainWindowTitle,
                        MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);
                }

                else
                {
                    MessageBox.Show("Возникла ошибка при отправлении данных:\n" + error.Message +
                        "\n\nКлиент не был отключен.", MainWindowTitle,
                        MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);
                }
            }
        }

        private void Client_DataReceived(object sender, DataFromDevice e)
        {
            try
            {
                lock (locker)
                {
                    Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Send,
                    new Action(delegate
                    {
                        try
                        {
                            TextBox_RX.AppendText(MainWindow.GlobalEncoding.GetString(e.RX));

                            if (CheckBox_NextLine.IsChecked == true)
                            {
                                TextBox_RX.AppendText("\n");
                            }

                            TextBox_RX.LineDown();
                            ScrollViewer_RX.ScrollToEnd();
                        }

                        catch (Exception error)
                        {
                            if (ErrorHandler != null)
                            {
                                ErrorHandler(this, new EventArgs());

                                MessageBox.Show("Возникла ошибка при приеме данных:\n" + error.Message +
                                    "\n\nКлиент был отключен.", MainWindowTitle,
                                    MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);
                            }

                            else
                            {
                                MessageBox.Show("Возникла ошибка при приеме данных:\n" + error.Message +
                                    "\n\nКлиент не был отключен.", MainWindowTitle,
                                    MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);
                            }
                        }
                    }));
                }
            }

            catch (Exception error)
            {
                if (ErrorHandler != null)
                {
                    ErrorHandler(this, new EventArgs());

                    MessageBox.Show("Возникла ошибка при приеме данных:\n" + error.Message +
                        "\n\nКлиент был отключен.", MainWindowTitle,
                        MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);
                }

                else
                {
                    MessageBox.Show("Возникла ошибка при приеме данных:\n" + error.Message +
                        "\n\nКлиент не был отключен.", MainWindowTitle,
                        MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);
                }
            }
        }

        private void Button_SaveAs_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (TextBox_RX.Text == "")
                {
                    MessageBox.Show("Поле приема не содержит данных.", MainWindowTitle,
                        MessageBoxButton.OK, MessageBoxImage.Warning);

                    TextBox_TX.Focus();

                    return;
                }

                Microsoft.Win32.SaveFileDialog dialog = new Microsoft.Win32.SaveFileDialog
                {
                    FileName = "HostResponse", // Имя по умолчанию
                    DefaultExt = ".txt",       // Расширение файла по умолчанию
                    Filter = "Text Document|*.txt" // Допустимые форматы файла
                };

                Nullable<bool> result = dialog.ShowDialog();

                if (result == true)
                {
                    using (FileStream Stream = new FileStream(dialog.FileName, FileMode.OpenOrCreate))
                    {
                        byte[] Data = Encoding.UTF8.GetBytes(TextBox_RX.Text);
                        Stream.Write(Data, 0, Data.Length);
                    }
                }
            }

            catch (Exception error)
            {
                MessageBox.Show("Ошибка при попытке сохранить данные поля приема в файл:\n\n" + error.Message, MainWindowTitle,
                    MessageBoxButton.OK, MessageBoxImage.Error);

                TextBox_TX.Focus();
            }
        }

        private void Button_ClearFieldRX_Click(object sender, RoutedEventArgs e)
        {
            TextBox_RX.Text = String.Empty;

            TextBox_TX.Focus();
        }        
    }
}
