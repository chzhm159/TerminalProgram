﻿using Core.Models.Modbus;
using Core.Models.Modbus.Message;

namespace Core.Tests.Modbus
{
    public class Protocol_ASCII_CreateTest
    {
        private ModbusMessage Message = new ModbusASCII_Message();

        private const UInt16 Polynom = 0xA001;

        [Fact]
        public void Test_Func_01()
        {
            CheckReadFunction(
                SelectedFunction: Function.ReadCoilStatus,
                SlaveID: 156,
                Address: 12,
                NumberOfRegisters: 5,
                CheckSum_IsEnable: true
                );
        }

        [Fact]
        public void Test_Func_02()
        {
            CheckReadFunction(
                SelectedFunction:  Function.ReadDiscreteInputs,
                SlaveID:           46,
                Address:           59,
                NumberOfRegisters: 4,
                CheckSum_IsEnable: false
                );
        }

        [Fact]
        public void Test_Func_03()
        {
            CheckReadFunction(
                SelectedFunction:  Function.ReadHoldingRegisters,
                SlaveID:           1,
                Address:           86,
                NumberOfRegisters: 1,
                CheckSum_IsEnable: true
                );
        }

        [Fact]
        public void Test_Func_04()
        {
            CheckReadFunction(
                SelectedFunction:  Function.ReadInputRegisters,
                SlaveID:           34,
                Address:           2,
                NumberOfRegisters: 3,
                CheckSum_IsEnable: false
                );
        }

        [Fact]
        public void Test_Func_05()
        {
            CheckSingleWriteFunction(
                SelectedFunction:  Function.ForceSingleCoil,
                SlaveID:           13,
                Address:           96,
                WriteData:         0x0056,
                CheckSum_IsEnable: true
                );
        }

        [Fact]
        public void Test_Func_06()
        {
            CheckSingleWriteFunction(
                SelectedFunction:  Function.PresetSingleRegister,
                SlaveID:           18,
                Address:           63,
                WriteData:         0xAED5,
                CheckSum_IsEnable: false
                );
        }

        [Fact]
        public void Test_Func_0F()
        {
            CheckMultiplyWriteFunction(
                SelectedFunction: Function.ForceMultipleCoils,
                SlaveID: 86,
                Address: 23,
                WriteData: new UInt16[] { 0x0000, 0x0000, 0xFF00 },
                CheckSum_IsEnable: true
                );
        }

        [Fact]
        public void Test_Func_16()
        {
            CheckMultiplyWriteFunction(
                SelectedFunction:  Function.PresetMultipleRegisters,
                SlaveID:           156,
                Address:           86,
                WriteData:         new UInt16[] { 0x00FF, 0x4521, 0x8500, 0x0058, 0xDAF8 },
                CheckSum_IsEnable: true
                );
        }



        // Общий функционал

        private void CheckReadFunction(ModbusReadFunction SelectedFunction,
            byte SlaveID, UInt16 Address, UInt16 NumberOfRegisters, bool CheckSum_IsEnable)
        {
            MessageData Data = new ReadTypeMessage(
                SlaveID,
                Address,
                NumberOfRegisters,
                CheckSum_IsEnable,
                Polynom
                );

            byte[] BytesArray_Actual = Message.CreateMessage(SelectedFunction, Data);

            byte[] AddressBytes = ModbusField.Get_Address(Address);
            byte[] NumberOfRegistersBytes = ModbusField.Get_NumberOfRegisters(NumberOfRegisters);

            byte[] DataBytes = new byte[]
            {
                SlaveID,
                SelectedFunction.Number,
                AddressBytes[1],
                AddressBytes[0],
                NumberOfRegistersBytes[1],
                NumberOfRegistersBytes[0]
            };

            byte[] BytesArray_Expected = Create_ASCII_Package(DataBytes, CheckSum_IsEnable);

            Assert.Equal(BytesArray_Actual, BytesArray_Expected);
        }


        private void CheckSingleWriteFunction(ModbusWriteFunction SelectedFunction,
            byte SlaveID, UInt16 Address, UInt16 WriteData, bool CheckSum_IsEnable)
        {
            UInt16[] WriteDataArray = new UInt16[] { WriteData };

            MessageData Data = new WriteTypeMessage(
                SlaveID,
                Address,
                WriteDataArray,
                CheckSum_IsEnable,
                Polynom
                );

            byte[] BytesArray_Actual = Message.CreateMessage(SelectedFunction, Data);

            byte[] AddressBytes = ModbusField.Get_Address(Address);
            byte[] WriteDataBytes = ModbusField.Get_WriteData(WriteDataArray);

            if (WriteDataBytes.Length != 2)
            {
                throw new Exception("При записи одного регистра поле данных должно содержать только 2 байта.");
            }

            byte[] DataBytes = new byte[]
            {
                SlaveID,
                SelectedFunction.Number,
                AddressBytes[1],
                AddressBytes[0],
                WriteDataBytes[0],
                WriteDataBytes[1]
            };

            byte[] BytesArray_Expected = Create_ASCII_Package(DataBytes, CheckSum_IsEnable);

            Assert.Equal(BytesArray_Expected, BytesArray_Actual);
        }

        private void CheckMultiplyWriteFunction(ModbusWriteFunction SelectedFunction,
            byte SlaveID, UInt16 Address, UInt16[] WriteData, bool CheckSum_IsEnable)
        {
            MessageData Data = new WriteTypeMessage(
                SlaveID,
                Address,
                WriteData,
                CheckSum_IsEnable,
                Polynom
                );

            byte[] BytesArray_Actual = Message.CreateMessage(SelectedFunction, Data);

            byte[] AddressBytes = ModbusField.Get_Address(Address);
            byte[] NumberOfRegisters = ModbusField.Get_NumberOfRegisters((UInt16)WriteData.Length);
            byte[] WriteDataBytes = ModbusField.Get_WriteData(WriteData);


            if (WriteDataBytes.Length != WriteData.Length * 2)
            {
                throw new Exception("Неправильное количество байт в поле данных.");
            }

            byte[] DataBytes = new byte[7 + WriteDataBytes.Length];

            DataBytes[0] = SlaveID;
            DataBytes[1] = SelectedFunction.Number;
            DataBytes[2] = AddressBytes[1];
            DataBytes[3] = AddressBytes[0];
            DataBytes[4] = NumberOfRegisters[1];
            DataBytes[5] = NumberOfRegisters[0];
            DataBytes[6] = (byte)WriteDataBytes.Length;

            Array.Copy(WriteDataBytes, 0, DataBytes, 7, WriteDataBytes.Length);

            byte[] BytesArray_Expected = Create_ASCII_Package(DataBytes, CheckSum_IsEnable);

            Assert.Equal(BytesArray_Expected, BytesArray_Actual);
        }

        private byte[] Create_ASCII_Package(byte[] MessageBytes, bool CheckSum_IsEnable)
        {
            byte[] DataBytes_ASCII = ModbusASCII_Message.ConvertArrayToASCII(MessageBytes);

            byte[] ResultArray;

            if (CheckSum_IsEnable)
            {
                ResultArray = new byte[5 + DataBytes_ASCII.Length];
            }

            else
            {
                ResultArray = new byte[3 + DataBytes_ASCII.Length];
            }

            // Символ начала кадра (префикс)
            ResultArray[0] = 0x3A;

            Array.Copy(DataBytes_ASCII, 0, ResultArray, 1, DataBytes_ASCII.Length);

            // LRC8
            if (CheckSum_IsEnable)
            {
                byte[] LRC8 = CheckSum.Calculate_LRC8(MessageBytes);
                ResultArray[ResultArray.Length - 4] = LRC8[0];
                ResultArray[ResultArray.Length - 3] = LRC8[1];
            }

            // Символы конца кадра
            ResultArray[ResultArray.Length - 2] = 0x0D;  // Предпоследний элемент
            ResultArray[ResultArray.Length - 1] = 0x0A;  // Последний элемент


            return ResultArray;
        }
    }
}
