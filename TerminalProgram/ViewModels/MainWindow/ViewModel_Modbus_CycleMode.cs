﻿using Core.Models.NoProtocol;
using Core.Models;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using TerminalProgram.Views.Protocols;
using TerminalProgram.Views;
using System.Reactive;
using System.Collections.ObjectModel;
using Core.Models.Modbus;
using System.Globalization;
using System.Reactive.Linq;
using Core.Models.Modbus.Message;
using System.Windows.Threading;
using System.Diagnostics;

namespace TerminalProgram.ViewModels.MainWindow
{
    internal class ViewModel_Modbus_CycleMode : ReactiveObject
    {
        private byte _slaveID = 1;

        public byte SlaveID
        {
            get => _slaveID;
            set => this.RaiseAndSetIfChanged(ref _slaveID, value);
        }

        private ObservableCollection<string> _readFunctions = new ObservableCollection<string>();

        public ObservableCollection<string> ReadFunctions
        {
            get => _readFunctions;
            set => this.RaiseAndSetIfChanged(ref _readFunctions, value);
        }

        private string? _selectedReadFunction;

        public string? SelectedReadFunction
        {
            get => _selectedReadFunction;
            set => this.RaiseAndSetIfChanged(ref _selectedReadFunction, value);
        }

        private int _period_ms;

        public int Period_ms
        {
            get => _period_ms;
            set => this.RaiseAndSetIfChanged(ref _period_ms, value);
        }

        private string? _address;

        public string? Address
        {
            get => _address;
            set => this.RaiseAndSetIfChanged(ref _address, value);
        }

        private bool _selectedNumberFormat_Hex;

        public bool SelectedNumberFormat_Hex
        {
            get => _selectedNumberFormat_Hex;
            set => this.RaiseAndSetIfChanged(ref _selectedNumberFormat_Hex, value);
        }

        private bool _selectedNumberFormat_Dec;

        public bool SelectedNumberFormat_Dec
        {
            get => _selectedNumberFormat_Dec;
            set => this.RaiseAndSetIfChanged(ref _selectedNumberFormat_Dec, value);
        }

        private UInt16 _numberOfRegisters = 1;

        public UInt16 NumberOfRegisters
        {
            get => _numberOfRegisters;
            set => this.RaiseAndSetIfChanged(ref _numberOfRegisters, value);
        }

        #region Button

        private const string Button_Content_Start = "Начать опрос";
        private const string Button_Content_Stop = "Остановить опрос";

        private string _button_Content = Button_Content_Start;

        public string Button_Content
        {
            get => _button_Content;
            set => this.RaiseAndSetIfChanged(ref _button_Content, value);
        }

        public ReactiveCommand<Unit, Unit> Command_Start_Stop_Polling { get; }

        #endregion


        private bool IsStart = false;

        private readonly ConnectedHost Model;

        private readonly Modbus_CycleMode ParentWindow;
        private readonly Action<string, MessageType> Message;
        private readonly Action UI_State_Work;
        private readonly Action UI_State_Wait;

        private NumberStyles NumberViewStyle;
        private UInt16 SelectedAddress = 0;

        private ModbusReadFunction? ReadFunction;
        private MessageData? Data;

        // Время в мс. взято с запасом.
        // Это время нужно для совместимости с методом Receive() из класса SerialPortClient
        private const int TimeForReadHandler = 100;

        public ViewModel_Modbus_CycleMode(
            Modbus_CycleMode ParentWindow,
            Action<string, MessageType> MessageBox,
            Action UI_State_Work,
            Action UI_State_Wait
            )
        {
            this.ParentWindow = ParentWindow;

            Message = MessageBox;

            this.UI_State_Work = UI_State_Work;
            this.UI_State_Wait = UI_State_Wait;

            Model = ConnectedHost.Model;

            Model.DeviceIsDisconnected += Model_DeviceIsDisconnected;

            Model.Modbus.Model_ErrorInCycleMode += Modbus_Model_ErrorInCycleMode;

            Period_ms = Model.Host_ReadTimeout + TimeForReadHandler;

            ParentWindow.Closing += (sender, e) => 
            { 
                Model.Modbus.CycleMode_Stop();
                Model.Modbus.Model_ErrorInCycleMode -= Modbus_Model_ErrorInCycleMode;
            };

            ParentWindow.KeyDown += ParentWindow_KeyDown_Handler;
                        
            Command_Start_Stop_Polling = ReactiveCommand.Create(Start_Stop_Handler);
            Command_Start_Stop_Polling.ThrownExceptions.Subscribe(error => Message.Invoke(error.Message, MessageType.Error));

            foreach (ModbusReadFunction element in Function.AllReadFunctions)
            {
                ReadFunctions.Add(element.DisplayedName);
            }

            SelectedReadFunction = Function.ReadInputRegisters.DisplayedName;

            SelectedNumberFormat_Hex = true;


            this.WhenAnyValue(x => x.Address)
                .WhereNotNull()
                .Select(x => StringValue.CheckNumber(x, NumberViewStyle, out SelectedAddress))
                .Subscribe(x => Address = x.ToUpper());

            this.WhenAnyValue(x => x.SelectedNumberFormat_Hex, x => x.SelectedNumberFormat_Dec)
                .Subscribe(values =>
                {
                    if (values.Item1 == true && values.Item2 == true)
                    {
                        return;
                    }

                    // Выбран шестнадцатеричный формат числа в полях Адрес и Данные
                    if (values.Item1)
                    {
                        SelectNumberFormat_Hex();
                    }

                    // Выбран десятичный формат числа в полях Адрес и Данные
                    else if (values.Item2)
                    {
                        SelectNumberFormat_Dec();
                    }
                });           

            this.UI_State_Wait.Invoke();
        }

        private void Model_DeviceIsDisconnected(object? sender, ConnectArgs e)
        {
            ParentWindow.Close();
        }

        private void Modbus_Model_ErrorInCycleMode(object? sender, string e)
        {
            if (IsStart)
            {
                UI_State_Wait.Invoke();

                Button_Content = Button_Content_Start;
                IsStart = false;
            }

            Message.Invoke(e, MessageType.Error);
        }

        public void SelectNumberFormat_Hex()
        {
            NumberViewStyle = NumberStyles.HexNumber;

            if (Address != null)
            {
                Address = Convert.ToInt32(Address).ToString("X");
            }            
        }

        private void SelectNumberFormat_Dec()
        {
            NumberViewStyle = NumberStyles.Number;

            if (Address != null)
            {
                Address = Int32.Parse(Address, NumberStyles.HexNumber).ToString();
            }           
        }        

        private void ParentWindow_KeyDown_Handler(object sender, KeyEventArgs e)
        {
            try
            {
                switch (e.Key)
                {
                    case Key.Enter:
                        Start_Stop_Handler();
                        break;

                    case Key.Escape:
                        ParentWindow.Close();
                        break;
                }
            }

            catch (Exception error)
            {
                Message.Invoke(error.Message, MessageType.Error);
            }
        }

        private void Start_Stop_Handler()
        {
            if (IsStart)
            {
                Model.Modbus.CycleMode_Stop();

                UI_State_Wait.Invoke();

                Button_Content = Button_Content_Start;
                IsStart = false;                
            }

            else
            {
                if (Model.HostIsConnect == false)
                {
                    Message.Invoke("Modbus клиент отключен.", MessageType.Error);
                    return;
                }

                if (Model.Modbus == null)
                {
                    Message.Invoke("Не инициализирован Modbus клиент.", MessageType.Error);
                    return;
                }                

                if (ViewModel_Modbus.ModbusMessageType == null)
                {
                    Message.Invoke("Не задан тип протокола Modbus.", MessageType.Error);
                    return;
                }

                if (Period_ms < Model.Host_ReadTimeout + TimeForReadHandler)
                {
                    Message.Invoke("Значение периода опроса не может быть меньше суммы таймаута чтения и " + 
                        TimeForReadHandler + " мс. (" + Model.Host_ReadTimeout + " мс. + " + TimeForReadHandler + "мс.)\n" +
                        "Таймаут чтения: " + Model.Host_ReadTimeout + " мс.", MessageType.Warning);

                    return;
                }

                if (Address == null || Address == String.Empty)
                {
                    Message.Invoke("Укажите адрес Modbus регистра.", MessageType.Warning);
                    return;
                }

                if (NumberOfRegisters == 0)
                {
                    Message.Invoke("Укажите количество регистров для чтения.", MessageType.Warning);
                    return;
                }

                ReadFunction = Function.AllReadFunctions.Single(x => x.DisplayedName == SelectedReadFunction);

                Data = new ReadTypeMessage(
                    SlaveID,
                    SelectedAddress,
                    NumberOfRegisters,
                    ViewModel_Modbus.ModbusMessageType is ModbusTCP_Message ? false : true,
                    ViewModel_Modbus.CRC16_Polynom);

                UI_State_Work.Invoke();

                Button_Content = Button_Content_Stop;
                IsStart = true;

                Model.Modbus.CycleMode_Period = Period_ms;
                Model.Modbus.CycleMode_Start(ReadModbusRegister);
            }
        }

        private void ReadModbusRegister()
        {
            try
            {
                if (ReadFunction == null)
                {
                    throw new Exception("Не выбрана функция чтения.");
                }

                if (Data == null)
                {
                    throw new Exception("Не сформированы данные для опроса.");
                }

                if (ViewModel_Modbus.ModbusMessageType == null)
                {
                    throw new Exception("Не выбран тип протокола Modbus.");
                }

                UInt16[] ModbusReadData = Model.Modbus.ReadRegister(
                                ReadFunction,
                                Data,
                                ViewModel_Modbus.ModbusMessageType,
                                out _,
                                out _);


                ViewModel_Modbus.AddResponseInDataGrid(new ModbusDataDisplayed()
                {
                    OperationID = ViewModel_Modbus.PackageNumber,
                    FuncNumber = ReadFunction.DisplayedNumber,
                    Address = SelectedAddress,
                    ViewAddress = ViewModel_Modbus.CreateViewAddress(SelectedAddress, ModbusReadData.Length),
                    Data = ModbusReadData,
                    ViewData = ViewModel_Modbus.CreateViewData(ModbusReadData)
                });
            }

            catch (ModbusException error)
            {
                ViewModel_Modbus.AddResponseInDataGrid(new ModbusDataDisplayed()
                {
                    OperationID = ViewModel_Modbus.PackageNumber,
                    FuncNumber = ReadFunction?.DisplayedNumber,
                    Address = SelectedAddress,
                    ViewAddress = ViewModel_Modbus.CreateViewAddress(SelectedAddress, 1),
                    Data = new UInt16[1],
                    ViewData = "Ошибка Modbus.\nКод: " + error.ErrorCode.ToString()
                });

                throw new Exception(
                    "Ошибка Modbus.\n\n" +
                    "Код функции: " + error.FunctionCode.ToString() + "\n" +
                    "Код ошибки: " + error.ErrorCode.ToString() + "\n\n" +
                    error.Message);
            }

            catch (Exception error)
            {
                throw new Exception("Возникла ошибка чтения Modbus регистра:\n\n" + error.Message);
            }
        }
    }
}
