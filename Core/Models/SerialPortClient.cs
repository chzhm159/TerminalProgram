﻿using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Core.Models
{
    public class SerialPortClient : IConnection
    {
        public event EventHandler<DataFromDevice>? DataReceived;

        public bool IsConnected
        {
            get
            {
                if (DeviceSerialPort == null || DeviceSerialPort.IsOpen == false)
                {
                    return false;
                }

                return true;
            }
        }

        public int WriteTimeout
        {
            get
            {
                if (DeviceSerialPort != null)
                {
                    return DeviceSerialPort.WriteTimeout;
                }

                return 0;
            }

            set
            {
                if (DeviceSerialPort != null)
                {
                    DeviceSerialPort.WriteTimeout = value;
                }
            }
        }

        public int ReadTimeout
        {
            get
            {
                if (DeviceSerialPort != null)
                {
                    return DeviceSerialPort.ReadTimeout;
                }

                return 0;
            }

            set
            {
                if (DeviceSerialPort != null)
                {
                    DeviceSerialPort.ReadTimeout = value;
                }
            }
        }

        private SerialPort? DeviceSerialPort = null;

        private Task? ReadThread = null;
        private CancellationTokenSource? ReadCancelSource = null;

        public void SetReadMode(ReadMode Mode)
        {
            switch(Mode)
            {
                case ReadMode.Async:

                    if (DeviceSerialPort != null && IsConnected)
                    {
                        ReadCancelSource = new CancellationTokenSource();

                        DeviceSerialPort.BaseStream.WriteTimeout = 500;
                        DeviceSerialPort.BaseStream.ReadTimeout = -1;   // Бесконечно

                        ReadThread = Task.Run(() => AsyncThread_Read(DeviceSerialPort.BaseStream, ReadCancelSource.Token));
                    }

                    break;

                case ReadMode.Sync:

                    if (DeviceSerialPort != null && IsConnected)
                    {
                        ReadCancelSource?.Cancel();

                        if (ReadThread != null)
                        {
                            Task.WaitAll(ReadThread);
                        }                        

                        DeviceSerialPort.DiscardInBuffer();
                        DeviceSerialPort.DiscardOutBuffer();
                    }

                    break;

                default:
                    throw new Exception("У клиента задан неизвестный режим чтения: " + Mode.ToString());
            }
        }

        public void Connect(ConnectionInfo Info)
        {
            try
            {
                if (Info.SerialPort == null)
                {
                    throw new Exception("Нет информации о настройках подключения по последовательному порту.");
                }

                if (Info.SerialPort.COM_Port == null ||
                    Info.SerialPort.BaudRate == null ||
                    Info.SerialPort.Parity == null ||
                    Info.SerialPort.DataBits == null ||
                    Info.SerialPort.StopBits == null)
                {
                    throw new Exception(
                        (Info.SerialPort.COM_Port == null ? "Не задан СОМ порт.\n" : "") +
                        (Info.SerialPort.BaudRate == null ? "Не задан BaudRate.\n" : "") +
                        (Info.SerialPort.Parity == null ? "Не задан Parity.\n" : "") +
                        (Info.SerialPort.DataBits == null ? "Не задан DataBits\n" : "") +
                        (Info.SerialPort.StopBits == null ? "Не задан StopBits\n" : "")
                        );
                }

                DeviceSerialPort = new SerialPort();

                if (Int32.TryParse(Info.SerialPort.BaudRate, out int BaudRate) == false)
                {
                    throw new Exception("Не удалось преобразовать значение BaudRate в целочисленное значение.\n" +
                        "Полученное значение BaudRate: " + Info.SerialPort.BaudRate);
                }

                Parity SelectedParity;

                switch (Info.SerialPort.Parity)
                {
                    case "None":
                        SelectedParity = Parity.None;
                        break;

                    case "Even":
                        SelectedParity = Parity.Even;
                        break;

                    case "Odd":
                        SelectedParity = Parity.Odd;
                        break;

                    default:
                        throw new Exception("Неправильно задано значение Parity.");
                }

                if (Int32.TryParse(Info.SerialPort.DataBits, out int DataBits) == false)
                {
                    throw new Exception("Не удалось преобразовать значение DataBits в целочисленное значение.\n" +
                        "Полученное значение DataBits: " + Info.SerialPort.DataBits);
                }

                StopBits SelectedStopBits;

                switch (Info.SerialPort.StopBits)
                {
                    case "0":
                        SelectedStopBits = StopBits.None;
                        break;

                    case "1":
                        SelectedStopBits = StopBits.One;
                        break;

                    case "1.5":
                        SelectedStopBits = StopBits.OnePointFive;
                        break;

                    case "2":
                        SelectedStopBits = StopBits.Two;
                        break;

                    default:
                        throw new Exception("Неправильно задано значение StopBits");
                }

                DeviceSerialPort.PortName = Info.SerialPort.COM_Port;
                DeviceSerialPort.BaudRate = BaudRate;
                DeviceSerialPort.Parity = SelectedParity;
                DeviceSerialPort.DataBits = DataBits;
                DeviceSerialPort.StopBits = SelectedStopBits;

                DeviceSerialPort.Open();
            }

            catch (Exception error)
            {
                DeviceSerialPort?.Close();

                string CommonMessage = "Не удалось подключиться к СОМ порту.\n\n";

                if (Info.SerialPort != null)
                {
                    throw new Exception(CommonMessage +
                        "Данные подключения:" + "\n" +
                        "COM - Port: " + Info.SerialPort.COM_Port + "\n" +
                        "BaudRate: " + Info.SerialPort.BaudRate + "\n" +
                        "Parity: " + Info.SerialPort.Parity + "\n" +
                        "DataBits: " + Info.SerialPort.DataBits + "\n" +
                        "StopBits: " + Info.SerialPort.StopBits + "\n\n" +
                        error.Message);
                }

                throw new Exception(CommonMessage + error.Message);
            }
        }

        public async Task Disconnect()
        {
            try
            {
                if (DeviceSerialPort != null && DeviceSerialPort.IsOpen)
                {
                    //ProtocolMode? SelectedProtocol = ((MainWindow)Application.Current.MainWindow).SelectedProtocol;

                    //if (SelectedProtocol != null && SelectedProtocol.CurrentReadMode == ReadMode.Async)
                    //{
                    //    ReadCancelSource?.Cancel();

                    //    if (ReadThread != null)
                    //    {
                    //        await Task.WhenAll(ReadThread).ConfigureAwait(false);

                    //        await Task.Delay(100);
                    //    }                        
                    //}                    

                    DeviceSerialPort.Close();
                }
            }

            catch(Exception error)
            {
                throw new Exception("Не удалось отключиться от СОМ порта.\n\n" + error.Message);
            }
         }

        public void Send(byte[] Message, int NumberOfBytes)
        {
            if (DeviceSerialPort == null)
            {
                return;
            }

            try
            {
                if (IsConnected)
                {
                    DeviceSerialPort.Write(Message, 0, NumberOfBytes);
                }                
            }

            catch (Exception error)
            {
                throw new Exception("Ошибка отправки данных:\n\n" + error.Message + "\n\n" +
                    "Таймаут передачи: " +
                    (DeviceSerialPort.WriteTimeout == Timeout.Infinite ?
                    "бесконечно" : (DeviceSerialPort.WriteTimeout.ToString() + " мс.")));
            }
        }

        public void Receive(byte[] Data)
        {
            if (DeviceSerialPort == null)
            {
                return;
            }

            int SavedTimeout = ReadTimeout;

            try
            {
                if (IsConnected)
                {
                    // Если ожидать ответа с помощью таймаута, то в случае получения данных примется
                    // только несколько первых байт. Поэтому таймаут реализуется с помощью задержки.
                    // Таким образом, буфер приема успеет заполниться.

                    ReadTimeout = 10;

                    Thread.Sleep(SavedTimeout);

                    DeviceSerialPort.Read(Data, 0, Data.Length);

                    ReadTimeout = SavedTimeout;
                }                
            }

            catch (Exception error)
            {
                throw new Exception("Ошибка приема данных:\n\n" + error.Message + "\n\n" +
                    "Таймаут приема: " + SavedTimeout + " мс.");
            }
        }

        private async Task AsyncThread_Read(Stream CurrentStream, CancellationToken ReadCancel)
        {
            try
            {
                byte[] BufferRX = new byte[50];

                int NumberOfReceiveBytes;

                Task<int> ReadResult;

                Task WaitCancel = Task.Run(async () =>
                {
                    while (ReadCancel.IsCancellationRequested == false)
                    {
                        await Task.Delay(50, ReadCancel);
                    }
                });

                Task CompletedTask;

                while (true)
                {
                    ReadCancel.ThrowIfCancellationRequested();

                    if (DataReceived != null && CurrentStream != null)
                    {
                        /// Метод асинхронного чтения у объекта класса Stream, 
                        /// который содержится в объекте класса SerialPort,
                        /// почему то не обрабатывает событие отмены у токена отмены.
                        /// Возможно это происходит из - за того что внутри метода происходят 
                        /// неуправляемые вызовы никоуровневого API.
                        /// Поэтому для отслеживания состояния токена отмены была создана задача WaitCancel.
                        
                        ReadResult = CurrentStream.ReadAsync(BufferRX, 0, BufferRX.Length, ReadCancel);

                        CompletedTask = await Task.WhenAny(ReadResult, WaitCancel).ConfigureAwait(false);

                        ReadCancel.ThrowIfCancellationRequested();

                        if (CompletedTask == WaitCancel)
                        {
                            throw new OperationCanceledException();
                        }
                                                
                        NumberOfReceiveBytes = ReadResult.Result;

                        DataFromDevice Data = new DataFromDevice(NumberOfReceiveBytes);

                        for (int i = 0; i < NumberOfReceiveBytes; i++)
                        {
                            Data.RX[i] = BufferRX[i];
                        }

                        ReadCancel.ThrowIfCancellationRequested();

                        DataReceived?.Invoke(this, Data);

                        Array.Clear(BufferRX, 0, NumberOfReceiveBytes);
                    }
                }
            }

            catch (OperationCanceledException)
            {
                //  Возникает при отмене задачи.
                //  По правилам отмены асинхронных задач это исключение можно игнорировать.
            }

            catch (IOException error)
            {
                //MessageBox.Show(
                //    "Возникла ошибка при асинхронном чтении у SerialPort клиента.\n\n" +
                //    error.Message +
                //    "\n\nТаймаут чтения: " + CurrentStream.ReadTimeout + " мс." +
                //    "\n\nЧтение данных прекращено. Возможно вам стоит изменить настройки и переподключиться.",
                //    "Ошибка",
                //    MessageBoxButton.OK, MessageBoxImage.Error);
            }

            catch (Exception error)
            {
                // TODO: Как правильно обработать это исключение?

                //MessageBox.Show("Возникла НЕОБРАБОТАННАЯ ошибка " +
                //    "при асинхронном чтении у SerialPort клиента.\n\n" + 
                //     (error.InnerException == null ? error.Message : error.InnerException.Message) +
                //    "\n\nКлиент был отключен.", "Ошибка",
                //    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
