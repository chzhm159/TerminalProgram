﻿namespace MessageBox_Core
{
    public enum MessageType
    {
        Error,
        Warning,
        Information
    }

    public enum MessageBoxResult
    {
        Default,
        Yes,
        No
    }

    public interface IMessageBox
    {
        void Show(string Message, MessageType Type);
        Task<MessageBoxResult> ShowYesNoDialog(string Message, MessageType Type);
    }
}
