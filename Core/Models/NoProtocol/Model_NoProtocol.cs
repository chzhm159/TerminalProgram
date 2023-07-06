﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Core.Clients;

namespace Core.Models.NoProtocol
{
    public class Model_NoProtocol
    {
        public event EventHandler<string>? Model_DataReceived;
        public event EventHandler<string>? Model_ErrorInReadThread;

        private IConnection? Client;

        public Model_NoProtocol(ConnectedHost Host)
        {
            Host.DeviceIsConnect += Host_DeviceIsConnect;
            Host.DeviceIsDisconnected += Host_DeviceIsDisconnected;
        }

        private void Host_DeviceIsConnect(object? sender, ConnectArgs e)
        {
            if (e.ConnectedDevice != null && e.ConnectedDevice.IsConnected)
            {
                Client = e.ConnectedDevice;

                Client.DataReceived += Client_DataReceived;
                Client.ErrorInReadThread += Client_ErrorInReadThread;
            }
        }

        private void Client_ErrorInReadThread(object? sender, string e)
        {
            Model_ErrorInReadThread?.Invoke(this, e);
        }

        private void Client_DataReceived(object? sender, DataFromDevice e)
        {
            Model_DataReceived?.Invoke(this, ConnectedHost.GlobalEncoding.GetString(e.RX));
        }

        private void Host_DeviceIsDisconnected(object? sender, ConnectArgs e)
        {
            Client = null;
        }

        public void Send(string StringMessage, bool CR_Enable, bool CF_Enable)
        {
            if (Client == null)
            {
                throw new Exception("Клиент не инициализирован.");
            }

            if (StringMessage == String.Empty)
            {
                throw new Exception("Буфер для отправления пуст. Введите отправляемое значение.");
            }

            List<byte> Message = new List<byte>(ConnectedHost.GlobalEncoding.GetBytes(StringMessage));

            if (CR_Enable == true)
            {
                Message.Add((byte)'\r');
            }

            if (CF_Enable == true)
            {
                Message.Add((byte)'\n');
            }

            Client.Send(Message.ToArray(), Message.Count);
        }
    }
}
