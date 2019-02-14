﻿using System;
using System.Threading.Tasks;
using Windows.Foundation;

namespace Device.Net.UWP
{
    public abstract class UWPDeviceBase<T> : UWPDeviceBase, IDevice
    {
        #region Fields
        private bool _IsClosing;
        private bool disposed;
        #endregion

        #region Protected Properties
        protected T _ConnectedDevice;
        #endregion

        #region Constructor
        protected UWPDeviceBase()
        {

        }

        protected UWPDeviceBase(string deviceId)
        {
            DeviceId = deviceId;
        }
        #endregion

        #region Protected Methods
        protected async Task GetDevice(string id)
        {
            var asyncOperation = FromIdAsync(id);
            var task = asyncOperation.AsTask();
            _ConnectedDevice = await task;
        }
        #endregion

        #region Protected Abstract Methods
        protected abstract IAsyncOperation<T> FromIdAsync(string id);
        #endregion

        #region Public Overrides
        public override async Task<byte[]> ReadAsync()
        {
            if (_IsReading)
            {
                throw new Exception("Reentry");
            }

            lock (_Chunks)
            {
                if (_Chunks.Count > 0)
                {
                    var retVal = _Chunks[0];
                    Tracer?.Trace(false, retVal);
                    _Chunks.RemoveAt(0);
                    return retVal;
                }
            }

            _IsReading = true;
            _TaskCompletionSource = new TaskCompletionSource<byte[]>();
            return await _TaskCompletionSource.Task;
        }
        #endregion

        #region Public Override Properties
        public override bool IsInitialized => _ConnectedDevice != null;
        #endregion

        #region Public Virtual Methods
        public override sealed void Dispose()
        {
            if (disposed) return;
            disposed = true;

            Close();
            _TaskCompletionSource?.Task?.Dispose();

            base.Dispose();

            GC.SuppressFinalize(this);
        }

        public void Close()
        {
            if (_IsClosing) return;

            _IsClosing = true;

            try
            {
                if (_ConnectedDevice is IDisposable disposable) disposable.Dispose();
                _ConnectedDevice = default(T);
            }
            catch (Exception ex)
            {
                Logger.Log("Error disposing", ex, nameof(UWPDeviceBase));
            }

            _IsClosing = false;
        }
        #endregion
    }
}
