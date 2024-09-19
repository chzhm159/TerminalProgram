﻿using ReactiveUI;
using System.Collections.ObjectModel;
using System.Globalization;
using ViewModels.Validation;

namespace ViewModels.ModbusClient.WriteFields
{
    public class SingleRegister_VM : ModbusDataFormatter, IWriteField_VM
    {
        private UInt16 _data = 0;

        private string? _viewData;

        public override string? ViewData
        {
            get => _viewData;
            set
            {
                this.RaiseAndSetIfChanged(ref _viewData, value);
                ValidateInput(nameof(ViewData), value);
            }
        }

        public NumberStyles DataFormat { get; private set; }

        private ObservableCollection<string> _formatItems = new ObservableCollection<string>()
        {
            DataFormatName_dec, DataFormatName_hex, DataFormatName_bin
        };

        public ObservableCollection<string> FormatItems
        {
            get => _formatItems;
        }

        private string? _selectedDataFormat;

        public string? SelectedDataFormat
        {
            get => _selectedDataFormat;
            set => SetDataFormat(value);
        }

        public SingleRegister_VM()
        {
            SelectedDataFormat = DataFormatName_hex;
        }

        public WriteData GetData()
        {
            byte[] data = BitConverter.GetBytes(_data);

            return new WriteData(data, 1);
        }

        public override void SetDataFormat(string? format)
        {
            switch (format)
            {
                case DataFormatName_dec:
                    DataFormat = NumberStyles.Number;
                    break;

                case DataFormatName_hex:
                    DataFormat = NumberStyles.HexNumber;
                    break;

                case DataFormatName_bin:
                    DataFormat = NumberStyles.BinaryNumber;
                    break;

                default:
                    throw new Exception("Неподдерживаемый формат числа: " + format);
            }

            _selectedDataFormat = format;

            ViewData = ConvertNumberToString(_data, DataFormat);
        }

        protected override IEnumerable<string> GetShortErrorMessages(string fieldName, string? value)
        {
            List<ValidateMessage> errors = new List<ValidateMessage>();

            if (string.IsNullOrEmpty(value))
            {
                return errors.Select(message => message.Short);
            }

            if (!StringValue.IsValidNumber(value, DataFormat, out _data))
            {
                switch (DataFormat)
                {
                    case NumberStyles.Number:
                        errors.Add(AllErrorMessages[DecError_UInt16]);
                        break;

                    case NumberStyles.HexNumber:
                        errors.Add(AllErrorMessages[HexError_UInt16]);
                        break;
                }
            }

            return errors.Select(message => message.Short);
        }
    }
}
