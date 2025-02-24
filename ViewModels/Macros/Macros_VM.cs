﻿using Core.Models.Settings;
using Core.Models.Settings.DataTypes;
using Core.Models.Settings.FileTypes;
using MessageBox_Core;
using ReactiveUI;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using ViewModels.Macros.DataTypes;
using ViewModels.Macros.MacrosItemContext;

namespace ViewModels.Macros
{
    public class Macros_VM : ReactiveObject
    {
        private const string ModeName_NoProtocol = "Без протокола";
        private const string ModeName_ModbusClient = "Modbus";

        private string? _modeName;

        public string? ModeName
        {
            get => _modeName;
            set => this.RaiseAndSetIfChanged(ref _modeName, value);
        }

        private ObservableCollection<MacrosViewItem_VM> _items = new ObservableCollection<MacrosViewItem_VM>();

        public ObservableCollection<MacrosViewItem_VM> Items
        {
            get => _items;
            set => this.RaiseAndSetIfChanged(ref _items, value);
        }

        public ReactiveCommand<Unit, Unit> Command_Import { get; set; }
        public ReactiveCommand<Unit, Unit> Command_Export { get; set; }
        public ReactiveCommand<Unit, Unit> Command_CreateMacros { get; set; }

        private MacrosNoProtocol? _noProtocolMacros;
        private MacrosModbus? _modbusMacros;

        private List<string> _allMacrosNames = new List<string>();

        private readonly IMessageBox _messageBox;
        private readonly Func<object?, Task<object?>> _openEditMacrosWindow;
        private readonly Func<string, Task<string?>> _getFolderPath;
        private readonly Func<string, Task<string?>> _getFilePath;

        private readonly Model_Settings _settings;

        public Macros_VM(
            IMessageBox messageBox, 
            Func<object?, Task<object?>> openEditMacrosWindow,
            Func<string, Task<string?>> getFolderPath_Handler,
            Func<string, Task<string?>> getFilePath_Handler)
        {
            _messageBox = messageBox;
            _openEditMacrosWindow = openEditMacrosWindow;
            _getFolderPath = getFolderPath_Handler;
            _getFilePath = getFilePath_Handler;

            _settings = Model_Settings.Model;

            Command_Import = ReactiveCommand.CreateFromTask(ImportMacros);
            Command_Import.ThrownExceptions.Subscribe(error => _messageBox.Show($"Ошибка при импорте макросов.\n\n{error.Message}", MessageType.Error));

            Command_Export = ReactiveCommand.CreateFromTask(ExportMacros);
            Command_Export.ThrownExceptions.Subscribe(error => _messageBox.Show($"Ошибка при экспорте макроса.\n\n{error.Message}", MessageType.Error));

            Command_CreateMacros = ReactiveCommand.CreateFromTask(CreateMacros);
            Command_CreateMacros.ThrownExceptions.Subscribe(error => _messageBox.Show($"Ошибка при создании макроса.\n\n{error.Message}", MessageType.Error));

            CommonUI_VM.ApplicationWorkModeChanged += CommonUI_VM_ApplicationWorkModeChanged;

            InitUI();
        }

        private void InitUI()
        {
            ModeName = GetModeName(CommonUI_VM.CurrentApplicationWorkMode);
            UpdateWorkspace(CommonUI_VM.CurrentApplicationWorkMode);
        }

        private string GetValidMacrosFileName()
        {
            if (_noProtocolMacros != null)
            {
                return _settings.FilePath_Macros_NoProtocol;
            }

            else if (_modbusMacros != null)
            {
                return _settings.FilePath_Macros_Modbus;
            }

            throw new Exception("Не выбран режим.");
        }

        private string GetModeName(ApplicationWorkMode mode)
        {
            switch (mode)
            {
                case ApplicationWorkMode.NoProtocol:
                    return ModeName_NoProtocol;

                case ApplicationWorkMode.ModbusClient:
                    return ModeName = ModeName_ModbusClient;

                default:
                    throw new NotImplementedException();
            }
        }

        private void CommonUI_VM_ApplicationWorkModeChanged(object? sender, ApplicationWorkMode e)
        {
            ModeName = GetModeName(e);

            UpdateWorkspace(e);
        }

        private void UpdateWorkspace(ApplicationWorkMode mode)
        {
            Items.Clear();

            _noProtocolMacros = null;
            _modbusMacros = null;

            _allMacrosNames.Clear();

            switch (mode)
            {
                case ApplicationWorkMode.NoProtocol:
                    _noProtocolMacros = BuildNoProtocolMacros();
                    break;

                case ApplicationWorkMode.ModbusClient:
                    _modbusMacros = BuildModbusMacros();
                    break;

                default:
                    throw new NotImplementedException();
            }
        }

        private MacrosNoProtocol BuildNoProtocolMacros()
        {
            MacrosNoProtocol macros = _settings.ReadOrCreateDefaultMacros<MacrosNoProtocol>();

            if (macros.Items == null)
            {
                return new MacrosNoProtocol()
                {
                    Items = new List<MacrosContent<MacrosCommandNoProtocol>>()
                };
            }

            foreach (var element in macros.Items)
            {
                IMacrosContext _macrosContext = new NoProtocolMacrosItemContext(element);

                BuildMacrosItem(_macrosContext.CreateContext());
            }

            return macros;
        }

        private MacrosModbus BuildModbusMacros()
        {
            MacrosModbus macros = _settings.ReadOrCreateDefaultMacros<MacrosModbus>();

            if (macros.Items == null)
            {
                return new MacrosModbus()
                {
                    Items = new List<MacrosContent<MacrosCommandModbus>>()
                };
            }

            foreach (var element in macros.Items)
            {
                IMacrosContext _macrosContext = new ModbusMacrosItemContext(element);

                BuildMacrosItem(_macrosContext.CreateContext());
            }

            return macros;
        }

        private async Task CreateMacros()
        {
            var currentMode = CommonUI_VM.CurrentApplicationWorkMode;

            var content = await _openEditMacrosWindow(null);

            if (content == null)
            {
                return;
            }

            AddMacrosItem(content);

            SaveMacros(currentMode);

            // На случай если режим будет изменен во время создания нового макроса
            if (currentMode.Equals(CommonUI_VM.CurrentApplicationWorkMode))
            {
                BuildMacrosItem(GetMacrosContextFrom(content).CreateContext());
            }
        }

        private async Task EditMacros(string name)
        {
            var currentMode = CommonUI_VM.CurrentApplicationWorkMode;

            object? initData;

            switch (currentMode)
            {
                case ApplicationWorkMode.NoProtocol:
                    initData = _noProtocolMacros?.Items?.Find(e => e.MacrosName == name);
                    break;

                case ApplicationWorkMode.ModbusClient:
                    initData = _modbusMacros?.Items?.Find(e => e.MacrosName == name);
                    break;

                default:
                    throw new NotImplementedException();
            }

            object? content = await _openEditMacrosWindow(initData);

            if (content == null)
            {
                return;
            }

            ChangeMacrosItem(name, content);

            SaveMacros(currentMode);

            // На случай если режим будет изменен во время создания нового макроса
            if (currentMode.Equals(CommonUI_VM.CurrentApplicationWorkMode))
            {
                ChangeMacrosViewItem(name, GetMacrosContextFrom(content).CreateContext());
            }
        }

        private void DeleteMacros(string name)
        {
            var viewItemToRemove = Items.First(item => item.Title == name);

            if (viewItemToRemove != null)
            {
                Items.Remove(viewItemToRemove);
            }

            _allMacrosNames.Remove(name);

            DeleteMacrosItem(name);

            SaveMacros(CommonUI_VM.CurrentApplicationWorkMode);
        }

        private void BuildMacrosItem(MacrosData itemData)
        {
            Items.Add(new MacrosViewItem_VM(itemData.Name, itemData.Action, EditMacros, DeleteMacros, _messageBox));
            _allMacrosNames.Add(itemData.Name);
        }

        private async Task ImportMacros()
        {
            ApplicationWorkMode workMode = CommonUI_VM.CurrentApplicationWorkMode;

            string modeName = GetModeName(workMode);

            if (await _messageBox.ShowYesNoDialog(
                "Внимание!!!\n\n" +
                $"При импорте файла макросов для режима \"{modeName}\" старые макросы будут удалены без возможности восстановления.\n\n" +
                "Продолжить?",
                MessageType.Warning) != MessageBoxResult.Yes)
            {
                return;
            }

            string? macrosFilePath = await _getFilePath($"Выбор файла для импорта макросов режима \"{modeName}\".");

            if (macrosFilePath != null)
            {
                string fileName = Path.GetFileName(macrosFilePath);

                string macrosValidFilePath = GetValidMacrosFileName();
                string validFileName = Path.GetFileName(macrosValidFilePath);

                if (fileName != validFileName)
                {
                    throw new Exception($"Некорректное имя файла макроса.\nОжидается имя \"{validFileName}\".");
                }

                try
                {
                    switch (workMode)
                    {
                        case ApplicationWorkMode.NoProtocol:
                            var macrosNoProtocol = _settings.ReadMacros<MacrosNoProtocol>(macrosFilePath);
                            break;

                        case ApplicationWorkMode.ModbusClient:
                            var macrosModbus = _settings.ReadMacros<MacrosModbus>(macrosFilePath);
                            break;

                        default:
                            throw new NotImplementedException();
                    }
                }

                catch (Exception error)
                {
                    throw new Exception($"Ошибка чтения файла.\n\n{error.Message}");
                }

                _settings.DeleteFile(macrosValidFilePath);

                _settings.CopyFile(macrosFilePath, macrosValidFilePath);

                // На случай если режим будет изменен во время импорта
                if (workMode.Equals(CommonUI_VM.CurrentApplicationWorkMode))
                {
                    UpdateWorkspace(workMode);
                }

                _messageBox.Show($"Файл с макросами для режима \"{modeName}\" успешно импортирован!", MessageType.Information);
            }
        }

        private async Task ExportMacros()
        {
            string? outputFilePath = await _getFolderPath("Выбор папки для экспорта файла макросов.");

            if (outputFilePath != null)
            {
                string macrosFileName = GetValidMacrosFileName();

                string outputFileName = Path.Combine(outputFilePath, Path.GetFileName(macrosFileName));

                _settings.CopyFile(macrosFileName, outputFileName);

                _messageBox.Show($"Экспорт прошел успешно!\n\nПуть к файлу:\n{outputFileName}", MessageType.Information);
            }
        }

        private IMacrosContext GetMacrosContextFrom(object? content)
        {
            if (content is MacrosContent<MacrosCommandNoProtocol> noProtocolContent)
            {
                return new NoProtocolMacrosItemContext(noProtocolContent);
            }

            else if (content is MacrosContent<MacrosCommandModbus> modbusContent)
            {
                return new ModbusMacrosItemContext(modbusContent);
            }

            throw new NotImplementedException($"Поддержка режима не реализована.");
        }

        private void SaveMacros(ApplicationWorkMode mode)
        {
            switch (mode)
            {
                case ApplicationWorkMode.NoProtocol:
                    _settings.SaveMacros(_noProtocolMacros);
                    break;

                case ApplicationWorkMode.ModbusClient:
                    _settings.SaveMacros(_modbusMacros);
                    break;

                default:
                    throw new NotImplementedException();
            }
        }

        private void AddMacrosItem(object content)
        {
            if (content is MacrosContent<MacrosCommandNoProtocol> noProtocolContent)
            {
                _noProtocolMacros?.Items?.Add(noProtocolContent);
            }

            else if (content is MacrosContent<MacrosCommandModbus> modbusContent)
            {
                _modbusMacros?.Items?.Add(modbusContent);
            }

            else
            {
                throw new NotImplementedException();
            }
        }

        private void DeleteMacrosItem(string name)
        {
            switch (CommonUI_VM.CurrentApplicationWorkMode)
            {
                case ApplicationWorkMode.NoProtocol:

                    var noProtocolItem = _noProtocolMacros?.Items?.First(macros => macros.MacrosName == name);

                    if (noProtocolItem != null)
                    {
                        _noProtocolMacros?.Items?.Remove(noProtocolItem);
                    }

                    break;

                case ApplicationWorkMode.ModbusClient:

                    var modbusItem = _modbusMacros?.Items?.First(macros => macros.MacrosName == name);

                    if (modbusItem != null)
                    {
                        _modbusMacros?.Items?.Remove(modbusItem);
                    }

                    break;

                default:
                    throw new NotImplementedException();
            }
        }

        private void ChangeMacrosItem(string oldName, object newContent)
        {
            if (newContent is MacrosContent<MacrosCommandNoProtocol> noProtocolContent)
            {
                var item = _noProtocolMacros?.Items?.First(item => item.MacrosName == oldName);

                if (item != null)
                {
                    item.MacrosName = noProtocolContent.MacrosName;
                    item.Commands = noProtocolContent.Commands;
                }
            }

            else if (newContent is MacrosContent<MacrosCommandModbus> modbusContent)
            {
                var item = _modbusMacros?.Items?.First(item => item.MacrosName == oldName);

                if (item != null)
                {
                    item.MacrosName = modbusContent.MacrosName;
                    item.Commands = modbusContent.Commands;
                }
            }

            else
            {
                throw new NotImplementedException();
            }
        }

        private void ChangeMacrosViewItem(string oldName, MacrosData newData)
        {
            var viewItem = Items.First(macros => macros.Title == oldName);

            if (viewItem != null)
            {
                viewItem.Title = newData.Name;
                viewItem.ClickAction = newData.Action;

                _allMacrosNames = Items.Select(item => item.Title).ToList();
            }
        }
    }
}
