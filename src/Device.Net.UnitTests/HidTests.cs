﻿using Hid.Net;
using Hid.Net.Windows;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Win32.SafeHandles;
using Moq;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Device.Net.UnitTests
{
    [TestClass]
    public class HidTests
    {
        #region Private Fields

        private readonly ILoggerFactory loggerFactory = LoggerFactory.Create(builder => _ = builder.AddDebug().AddConsole().SetMinimumLevel(LogLevel.Trace));

        #endregion Private Fields


        #region Public Methods

        [TestMethod]
        public void TestDeviceIdInvalidException()
        {
            try
            {
                _ = new WindowsHidHandler(null, readTransferTransform: (a) => default, writeTransferTransform: (a, b) => default);
            }
            catch (ArgumentNullException ane)
            {
                Assert.AreEqual("deviceId", ane.ParamName);
                return;
            }

            Assert.Fail();
        }


        [TestMethod]
        public async Task TestInitializeHidDeviceReadOnly()
        {
            var windowsHidDevice = await InitializeWindowsHidDevice(true);
            Assert.AreEqual(true, windowsHidDevice.IsReadOnly);
        }

        [TestMethod]
        public async Task TestInitializeHidDeviceWriteable()
        {
            var windowsHidDevice = await InitializeWindowsHidDevice(false);
            Assert.AreEqual(false, windowsHidDevice.IsReadOnly);
        }
        [TestMethod]
        public Task TestWriteAndReadFromTrezorHid() => IntegrationTests.TestWriteAndReadFromTrezor(
            new FilterDeviceDefinition(vendorId: 0x534C, productId: 0x0001, label: "Trezor One Firmware 1.6.x", usagePage: 65280)
            .GetHidDeviceFactory(
                loggerFactory,
                //Default the read report to 0.
                //I.e. stick 0 at index 0 and shift the rest of the array to the right
                0,
                (readReport)
                //We expect to get back 64 bytes but ReadAsync would normally add the Report Id back index 0
                //In the case of Trezor we just take the 64 bytes and don't put the Report Id back at index 0
                => new TransferResult(readReport.TransferResult.Data, readReport.TransferResult.BytesTransferred)
                )
            ,
            64,
            65
            );

        #endregion Public Methods

        #region Private Methods

        private static async Task<WindowsHidHandler> InitializeWindowsHidDevice(bool isReadonly)
        {
            const string deviceId = "test";
            var hidService = new Mock<IHidApiService>();
            var invalidSafeFileHandle = new SafeFileHandle((IntPtr)(-1), true);
            var validSafeFileHandle = new SafeFileHandle((IntPtr)100, true);

            _ = hidService.Setup(s => s.CreateReadConnection(deviceId, Windows.FileAccessRights.GenericRead)).Returns(validSafeFileHandle);
            _ = hidService.Setup(s => s.CreateWriteConnection(deviceId)).Returns(!isReadonly ? validSafeFileHandle : invalidSafeFileHandle);
            _ = hidService.Setup(s => s.GetDeviceDefinition(deviceId, validSafeFileHandle)).Returns(new ConnectedDeviceDefinition(deviceId, DeviceType.Hid, readBufferSize: 64, writeBufferSize: 64));

            var readStream = new Mock<Stream>();
            _ = readStream.Setup(s => s.CanRead).Returns(true);
            _ = hidService.Setup(s => s.OpenRead(It.IsAny<SafeFileHandle>(), It.IsAny<ushort>())).Returns(readStream.Object);

            var writeStream = new Mock<Stream>();
            _ = readStream.Setup(s => s.CanWrite).Returns(!isReadonly);
            _ = hidService.Setup(s => s.OpenWrite(It.IsAny<SafeFileHandle>(), It.IsAny<ushort>())).Returns(readStream.Object);

            var loggerFactory = new Mock<ILoggerFactory>();
            var logger = new Mock<ILogger<HidDevice>>();
            _ = logger.Setup(l => l.BeginScope(It.IsAny<It.IsAnyType>())).Returns(new Mock<IDisposable>().Object);

            _ = loggerFactory.Setup(f => f.CreateLogger(It.IsAny<string>())).Returns(logger.Object);

            var windowsHidDevice = new WindowsHidHandler(deviceId, loggerFactory: loggerFactory.Object, readTransferTransform: (a) => default, writeTransferTransform: (a, b) => default, hidApiService: hidService.Object);
            await windowsHidDevice.InitializeAsync();

            //TODO: Fix this

            if (!isReadonly)
            {
                //UnitTests.CheckLogMessageText(logger, Messages.SuccessMessageReadFileStreamOpened, LogLevel.Information, Times.Once());

                //logger.Received().Log(Messages.SuccessMessageReadFileStreamOpened, nameof(WindowsHidDevice), null, LogLevel.Information);
            }
            else
            {
                //logger.Received().Log(Messages.WarningMessageOpeningInReadonlyMode(deviceId), nameof(WindowsHidDevice), null, LogLevel.Warning);
            }

            hidService.Verify(s => s.OpenRead(It.IsAny<SafeFileHandle>(), It.IsAny<ushort>()));

            if (!isReadonly)
            {
                hidService.Verify(s => s.OpenWrite(It.IsAny<SafeFileHandle>(), It.IsAny<ushort>()));
            }

            return windowsHidDevice;
        }

        #endregion Private Methods

    }
}