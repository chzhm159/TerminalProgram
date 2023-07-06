﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reactive;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using ReactiveUI;
using Core.Models;
using Core.Models.Modbus;
using Core.Models.Modbus.Message;
using System.Globalization;
using System.Reactive.Linq;
using TerminalProgram.Views;
using Core.Clients;

namespace TerminalProgram.ViewModels.MainWindow
{
    public class ModbusDataDisplayed
    {
        public UInt16 OperationID { get; set; }
        public string? FuncNumber { get; set; }
        public UInt16 Address { get; set; }
        public string? ViewAddress { get; set; }
        public UInt16[]? Data { get; set; }
        public string? ViewData { get; set; }
    }

    internal class ViewModel_Modbus : ReactiveObject
    {
        #region Properties

        private const string ModbusMode_Name_Default = "не определен";

        private string? _modbusMode_Name;

        public string? ModbusMode_Name
        {
            get => _modbusMode_Name;
            set => this.RaiseAndSetIfChanged(ref _modbusMode_Name, value);
        }

        private readonly ObservableCollection<ModbusDataDisplayed> _dataDisplayedList =
            new ObservableCollection<ModbusDataDisplayed>();

        public ObservableCollection<ModbusDataDisplayed> DataDisplayedList
        {
            get => _dataDisplayedList;
        }

        private string? _slaveID;

        public string? SlaveID
        {
            get => _slaveID;
            set => this.RaiseAndSetIfChanged(ref _slaveID, value);
        }

        private bool _crc16_Enable;

        public bool CRC16_Enable
        {
            get => _crc16_Enable;
            set => this.RaiseAndSetIfChanged(ref _crc16_Enable, value);
        }

        private bool _crc16_IsVisible;

        public bool CRC16_IsVisible
        {
            get => _crc16_IsVisible;
            set => this.RaiseAndSetIfChanged(ref _crc16_IsVisible, value);
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

        private string? _numberFormat;

        public string? NumberFormat
        {
            get => _numberFormat;
            set => this.RaiseAndSetIfChanged(ref _numberFormat, value);
        }

        private string? _address;

        public string? Address
        {
            get => _address;
            set => this.RaiseAndSetIfChanged(ref _address, value);
        }

        private string? _numberOfRegisters;

        public string? NumberOfRegisters
        {
            get => _numberOfRegisters;
            set => this.RaiseAndSetIfChanged(ref _numberOfRegisters, value);
        }

        private string? _writeData;

        public string? WriteData
        {
            get => _writeData;
            set => this.RaiseAndSetIfChanged(ref _writeData, value);
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

        private ObservableCollection<string> _writeFunctions = new ObservableCollection<string>();

        public ObservableCollection<string> WriteFunctions
        {
            get => _writeFunctions;
            set => this.RaiseAndSetIfChanged(ref _writeFunctions, value);
        }

        private string? _selectedWriteFunction;

        public string? SelectedWriteFunction
        {
            get => _selectedWriteFunction;
            set => this.RaiseAndSetIfChanged(ref _selectedWriteFunction, value);
        }

        #endregion

        #region Commands

        public ReactiveCommand<Unit, Unit> Command_Write { get; }
        public ReactiveCommand<Unit, Unit> Command_Read { get; }
        public ReactiveCommand<Unit, Unit> Command_ClearDataGrid { get; }

        #endregion

        private readonly ConnectedHost Model;

        private readonly Action<string, MessageType> Message;
        private readonly Action SetUI_Connected;
        private readonly Action SetUI_Disconnected;
        private readonly Action<ModbusDataDisplayed> DataGrid_ScrollTo;

        private ModbusMessage? ModbusMessageType;

        private readonly List<UInt16> WriteBuffer = new List<UInt16>();

        private NumberStyles NumberViewStyle;

        private UInt16 PackageNumber = 0;

        private byte SelectedSlaveID = 0;
        private UInt16 SelectedAddress = 0;
        private UInt16 SelectedNumberOfRegisters = 1;

        private const UInt16 CRC_Polynom = 0xA001;

        private ModbusFunction? CurrentFunction;

        public ViewModel_Modbus(
            Action<string, MessageType> MessageBox,
            Action UI_Connected_Handler,
            Action UI_Disconnected_Handler,
            Action<ModbusDataDisplayed> UI_DataGrid_ScrollTo_Handler)
        {
            Message = MessageBox;

            SetUI_Connected = UI_Connected_Handler;
            SetUI_Disconnected = UI_Disconnected_Handler;

            DataGrid_ScrollTo = UI_DataGrid_ScrollTo_Handler;

            Model = ConnectedHost.Model;

            SetUI_Disconnected.Invoke();

            Model.DeviceIsConnect += Model_DeviceIsConnect;
            Model.DeviceIsDisconnected += Model_DeviceIsDisconnected;


            /****************************************************/
            //
            // Первоначальная настройка UI
            //
            /****************************************************/


            ModbusMode_Name = ModbusMode_Name_Default;

            CRC16_Enable = true;
            CRC16_IsVisible = true;

            SelectedNumberFormat_Hex = true;

            foreach (ModbusReadFunction element in Function.AllReadFunctions)
            {
                ReadFunctions.Add(element.DisplayedName);
            }

            SelectedReadFunction = Function.ReadInputRegisters.DisplayedName;

            foreach (ModbusWriteFunction element in Function.AllWriteFunctions)
            {
                WriteFunctions.Add(element.DisplayedName);
            }

            SelectedWriteFunction = Function.PresetSingleRegister.DisplayedName;


            /****************************************************/
            //
            // Настройка свойств и команд модели отображения
            //
            /****************************************************/


            Command_ClearDataGrid = ReactiveCommand.Create(DataDisplayedList.Clear);
            Command_ClearDataGrid.ThrownExceptions.Subscribe(error => Message.Invoke("Ошибка очистки содержимого таблицы.\n\n" + error.Message, MessageType.Error));

            Command_Write = ReactiveCommand.Create(Modbus_Write);
            Command_Read = ReactiveCommand.Create(Modbus_Read);


            this.WhenAnyValue(x => x.SlaveID)
                .WhereNotNull()
                .Select(x => StringValue.CheckNumber(x, NumberStyles.Number, out SelectedSlaveID))
                .Subscribe(x => SlaveID = x);

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

            this.WhenAnyValue(x => x.Address)
                .WhereNotNull()
                .Select(x => StringValue.CheckNumber(x, NumberViewStyle, out SelectedAddress))
                .Subscribe(x => Address = x.ToUpper());

            this.WhenAnyValue(x => x.NumberOfRegisters)
                .WhereNotNull()
                .Select(x => StringValue.CheckNumber(x, NumberStyles.Number, out SelectedNumberOfRegisters))
                .Subscribe(x => NumberOfRegisters = x);

            this.WhenAnyValue(x => x.SelectedWriteFunction)
                .WhereNotNull()
                .Where(x => x != String.Empty)
                .Subscribe(_ => WriteData = String.Empty);

            this.WhenAnyValue(x => x.WriteData)
                .WhereNotNull()
                .Subscribe(x => WriteData = WriteData_TextChanged(x));
        }

        private void SelectNumberFormat_Hex()
        {
            NumberFormat = "(hex)";
            NumberViewStyle = NumberStyles.HexNumber;

            if (Address != null)
            {
                Address = Convert.ToInt32(Address).ToString("X");
            }            

            WriteData = ConvertDataTextIn(NumberViewStyle, WriteData);
        }

        private void SelectNumberFormat_Dec()
        {
            NumberFormat = "(dec)";
            NumberViewStyle = NumberStyles.Number;

            if (Address != null)
            {
                Address = Int32.Parse(Address, NumberStyles.HexNumber).ToString();
            }            

            WriteData = ConvertDataTextIn(NumberViewStyle, WriteData);
        }

        private string ConvertDataTextIn(NumberStyles Style, string? Text)
        {
            if (Text == null)
            {
                return String.Empty;
            }

            string[] SplitString = Text.Split(' ');

            string[] Values = SplitString.Where(element => element != "").ToArray();

            string DataString = "";

            if (Style == NumberStyles.Number)
            {
                foreach (string element in Values)
                {
                    DataString += Int32.Parse(element, NumberStyles.HexNumber).ToString() + " ";
                }
            }

            else if (Style == NumberStyles.HexNumber)
            {
                foreach (string element in Values)
                {
                    DataString += Convert.ToInt32(element).ToString("X") + " ";
                }
            }

            return DataString;
        }

        private string WriteData_TextChanged(string EnteredText)
        {
            try
            {
                if (SelectedWriteFunction == Function.PresetMultipleRegister.DisplayedName)
                {
                    WriteBuffer.Clear();

                    string[] SplitString = EnteredText.Split(' ');

                    string[] Values = SplitString.Where(element => element != "").ToArray();

                    UInt16 Buffer = 0;

                    for (int i = 0; i < Values.Length; i++)
                    {
                        Values[i] = StringValue.CheckNumber(Values[i], NumberViewStyle, out Buffer);
                        WriteBuffer.Add(Buffer);
                    }

                    // Если при второй итерации последний элемент в SplitString равен "",
                    // то в конце был пробел.
                    return string.Join(" ", Values).ToUpper() + (SplitString.Last() == "" ? " " : "");
                }

                else
                {
                    return StringValue.CheckNumber(WriteData, NumberViewStyle, out UInt16 _).ToUpper();
                }
            }

            catch (Exception error)
            {
                Message.Invoke("Возникла ошибка при изменении текста в поле \"Данные\":\n\n" +
                    error.Message, MessageType.Error);

                return String.Empty;
            }
        }

        private void Model_DeviceIsConnect(object? sender, ConnectArgs e)
        {
            if (e.ConnectedDevice is IPClient)
            {
                ModbusMessageType = new ModbusTCP_Message();

                CRC16_IsVisible = false;
            }

            else if (e.ConnectedDevice is SerialPortClient)
            {
                ModbusMessageType = new ModbusRTU_Message();

                CRC16_IsVisible = true;
            }

            else
            {
                Message.Invoke("Задан неизвестный тип подключения.", MessageType.Error);
                return;
            }

            WriteBuffer.Clear();

            ModbusMode_Name = ModbusMessageType.ProtocolName;

            SetUI_Connected.Invoke();

            DataDisplayedList.Clear();
        }

        private void Model_DeviceIsDisconnected(object? sender, ConnectArgs e)
        {
            SetUI_Disconnected.Invoke();

            CRC16_IsVisible = true;

            ModbusMode_Name = ModbusMode_Name_Default;

            PackageNumber = 0;
        }

        private void Modbus_Write()
        {
            try
            {
                if (Model.Modbus == null)
                {
                    Message.Invoke("Не инициализирован Modbus клиент.", MessageType.Warning);
                    return;
                }

                if (ModbusMessageType == null)
                {
                    Message.Invoke("Не задан тип протокола Modbus.", MessageType.Warning);
                    return;
                }

                if (SlaveID == null || SlaveID == String.Empty)
                {
                    Message.Invoke("Укажите Slave ID.", MessageType.Warning);
                    return;
                }

                if (Address == null || Address == String.Empty)
                {
                    Message.Invoke("Укажите адрес Modbus регистра.", MessageType.Warning);
                    return;
                }

                if (WriteData == null || WriteData == String.Empty)
                {
                    Message.Invoke("Укажите данные для записи в Modbus регистр.", MessageType.Warning);
                    return;
                }

                ModbusWriteFunction WriteFunction = Function.AllWriteFunctions.Single(x => x.DisplayedName == SelectedWriteFunction);

                CurrentFunction = WriteFunction;

                UInt16[] ModbusWriteData;

                if (WriteFunction == Function.PresetMultipleRegister)
                {
                    ModbusWriteData = WriteBuffer.ToArray();
                }

                else
                {
                    ModbusWriteData = new UInt16[1];

                    StringValue.CheckNumber(WriteData, NumberViewStyle, out ModbusWriteData[0]);
                }
                                
                MessageData Data = new WriteTypeMessage(
                    SelectedSlaveID,
                    SelectedAddress,
                    ModbusWriteData,
                    ModbusMessageType is ModbusTCP_Message ? false : CRC16_Enable,
                    CRC_Polynom);


                Model.Modbus.WriteRegister(
                    WriteFunction, 
                    Data,
                    ModbusMessageType);


                DataDisplayedList.Add(new ModbusDataDisplayed()
                {
                    OperationID = PackageNumber,
                    FuncNumber = WriteFunction.DisplayedNumber,
                    Address = SelectedAddress,
                    ViewAddress = CreateViewAddress(SelectedAddress, ModbusWriteData.Length),
                    Data = ModbusWriteData,
                    ViewData = CreateViewData(ModbusWriteData)
                });

                DataGrid_ScrollTo?.Invoke(DataDisplayedList.Last());

                PackageNumber++;
            }

            catch (ModbusException error)
            {
                ModbusErrorHandler(error);
            }

            catch (Exception error)
            {
                Message.Invoke("Возникла ошибка при нажатии на кнопку \"Записать\":\n\n" + error.Message, MessageType.Error);
            }
        }

        private void Modbus_Read()
        {
            try
            {
                if (Model.Modbus == null)
                {
                    Message.Invoke("Не инициализирован Modbus клиент.", MessageType.Warning);
                    return;
                }

                if (ModbusMessageType == null)
                {
                    Message.Invoke("Не задан тип протокола Modbus.", MessageType.Warning);
                    return;
                }

                if (SlaveID == null || SlaveID == String.Empty)
                {
                    Message.Invoke("Укажите Slave ID.", MessageType.Warning);
                    return;
                }

                if (Address == null || Address == String.Empty)
                {
                    Message.Invoke("Укажите адрес Modbus регистра.", MessageType.Warning);
                    return;
                }

                if (NumberOfRegisters == null || NumberOfRegisters == String.Empty)
                {
                    Message.Invoke("Укажите количество регистров для чтения.", MessageType.Warning);
                    return;
                }

                if (SelectedNumberOfRegisters < 1)
                {
                    Message.Invoke("Сколько, сколько регистров вы хотите прочитать? :)", MessageType.Warning);
                    return;
                }

                ModbusReadFunction ReadFunction = Function.AllReadFunctions.Single(x => x.DisplayedName == SelectedReadFunction);

                CurrentFunction = ReadFunction;


                MessageData Data = new ReadTypeMessage(
                    SelectedSlaveID,
                    SelectedAddress,
                    SelectedNumberOfRegisters,
                    ModbusMessageType is ModbusTCP_Message ? false : CRC16_Enable,
                    CRC_Polynom);


                UInt16[] ModbusReadData = Model.Modbus.ReadRegister(
                                ReadFunction,
                                Data,
                                ModbusMessageType);


                DataDisplayedList.Add(new ModbusDataDisplayed()
                {
                    OperationID = PackageNumber,
                    FuncNumber = ReadFunction.DisplayedNumber,
                    Address = SelectedAddress,
                    ViewAddress = CreateViewAddress(SelectedAddress, ModbusReadData.Length),
                    Data = ModbusReadData,
                    ViewData = CreateViewData(ModbusReadData)
                });

                DataGrid_ScrollTo?.Invoke(DataDisplayedList.Last());

                PackageNumber++;
            }

            catch (ModbusException error)
            {
                ModbusErrorHandler(error);
            }

            catch (Exception error)
            {
                Message.Invoke("Возникла ошибка при нажатии нажатии на кнопку \"Прочитать\": \n\n" + error.Message, MessageType.Error);
            }
        }

        private string CreateViewAddress(UInt16 StartAddress, int NumberOfRegisters)
        {
            string DisplayedString = String.Empty;

            UInt16 CurrentAddress = StartAddress;

            for (int i = 0; i < NumberOfRegisters; i++)
            {
                DisplayedString += "0x" + CurrentAddress.ToString("X") +
                    " (" + CurrentAddress.ToString() + ")";

                if (i != NumberOfRegisters - 1)
                {
                    DisplayedString += "\n";
                }

                CurrentAddress++;
            }

            return DisplayedString;
        }

        private string CreateViewData(UInt16[] ModbusData)
        {
            string DisplayedString = String.Empty;

            for (int i = 0; i < ModbusData.Length; i++)
            {
                DisplayedString += "0x" + ModbusData[i].ToString("X") +
                    " (" + ModbusData[i].ToString() + ")";

                if (i != ModbusData.Length - 1)
                {
                    DisplayedString += "\n";
                }
            }

            return DisplayedString;
        }

        private void ModbusErrorHandler(ModbusException error)
        {
            DataDisplayedList.Add(new ModbusDataDisplayed()
            {
                OperationID = PackageNumber,
                FuncNumber = CurrentFunction?.DisplayedNumber,
                Address = SelectedAddress,
                ViewAddress = CreateViewAddress(SelectedAddress, 1),
                Data = new UInt16[1],
                ViewData = "Ошибка Modbus.\nКод: " + error.ErrorCode.ToString()
            });

            DataGrid_ScrollTo?.Invoke(DataDisplayedList.Last());

            PackageNumber++;

            Message.Invoke(
                "Ошибка Modbus.\n\n" +
                "Код функции: " + error.FunctionCode.ToString() + "\n" +
                "Код ошибки: " + error.ErrorCode.ToString() + "\n\n" +
                error.Message, 
                MessageType.Error);
        }
    }
}
