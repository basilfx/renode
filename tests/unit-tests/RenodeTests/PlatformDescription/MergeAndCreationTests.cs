﻿﻿//
// Copyright (c) Antmicro
//
// This file is part of the Renode project.
// Full license details are defined in the 'LICENSE' file.
//
using System;
using System.Linq;
using Emul8.Core;
using Emul8.Peripherals.CPU;
using Antmicro.Renode.PlatformDescription;
using Antmicro.Renode.PlatformDescription.Syntax;
using Emul8.Utilities;
using Moq;
using NUnit.Framework;
using UnitTests.Mocks;

namespace Antmicro.Renode.UnitTests.PlatformDescription
{
    [TestFixture]
    public class MergeAndCreationTests
    {
        [Test]
        public void ShouldUpdateRegistrationPoint()
        {
            var source = @"
register1: Emul8.UnitTests.Mocks.NullRegister @ sysbus <0, 100>
register2: Emul8.UnitTests.Mocks.NullRegister @ sysbus new Emul8.Peripherals.Bus.BusRangeRegistration { range: <100, +100> }
cpu: UnitTests.Mocks.MockCPU @ register1
cpu: @register2";

            ProcessSource(source);
            ICPU peripheral;
            Assert.IsTrue(machine.TryGetByName("sysbus.register2.cpu", out peripheral));
            Assert.IsFalse(machine.TryGetByName("sysbus.register1.cpu", out peripheral));
        }

        [Test]
        public void ShouldCancelRegistration()
        {
            var source = @"
register: Emul8.UnitTests.Mocks.NullRegister @ sysbus <0, 100>
cpu: UnitTests.Mocks.MockCPU @ register
cpu: @none";

            ProcessSource(source);
            ICPU peripheral;
            Assert.IsFalse(machine.TryGetByName("sysbus.register.cpu", out peripheral));
        }

        [Test]
        public void ShouldHandleRegistrationInReverseOrder()
        {
            var source = @"
cpu: UnitTests.Mocks.MockCPU @ register
register: Emul8.UnitTests.Mocks.NullRegister @ sysbus <0, 100>
";

            ProcessSource(source);
            ICPU peripheral;
            Assert.IsTrue(machine.TryGetByName("sysbus.register.cpu", out peripheral));
        }

        [Test]
        public void ShouldHandleAlias()
        {
            var source = @"
cpu: UnitTests.Mocks.MockCPU @ sysbus as ""otherName""";

            ProcessSource(source);
            ICPU peripheral;
            Assert.IsTrue(machine.TryGetByName("sysbus.otherName", out peripheral));
            Assert.IsFalse(machine.TryGetByName("sysbus.cpu", out peripheral));
        }

        [Test]
        public void ShouldUpdateProperty()
        {
            var source = @"
cpu: UnitTests.Mocks.MockCPU @ sysbus
    Placeholder: ""one""
    EnumValue: TwoStateEnum.Two

cpu:
    Placeholder: ""two""";

            ProcessSource(source);
            MockCPU mock;
            Assert.IsTrue(machine.TryGetByName("sysbus.cpu", out mock));
            Assert.AreEqual("two", mock.Placeholder);
            Assert.AreEqual(TwoStateEnum.Two, mock.EnumValue);
        }

        [Test]
        public void ShouldHandleEscapedOuoteInString()
        {
            var source = @"
cpu: UnitTests.Mocks.MockCPU @ sysbus
    Placeholder: ""one with \""escaped\"" quote\""""";

            ProcessSource(source);
            MockCPU mock;
            Assert.IsTrue(machine.TryGetByName("sysbus.cpu", out mock));
            Assert.AreEqual("one with \"escaped\" quote\"", mock.Placeholder);
        }

        [Test]
        public void ShouldHandleNoneInProperty()
        {
            var source = @"
cpu: UnitTests.Mocks.MockCPU @ sysbus
    EnumValue: TwoStateEnum.Two

cpu:
    EnumValue: none";

            ProcessSource(source);
            MockCPU mock;
            Assert.IsTrue(machine.TryGetByName("sysbus.cpu", out mock));
            Assert.AreEqual(TwoStateEnum.One, mock.EnumValue);
        }

        [Test]
        public void ShouldHandleNoneInCtorParam()
        {
            var source = @"
peripheral: UnitTests.Mocks.EmptyPeripheral @ sysbus <0, 1>
    value: 1

peripheral:
    value: none";

            ProcessSource(source);
            EmptyPeripheral peripheral;
            Assert.IsTrue(machine.TryGetByName("sysbus.peripheral", out peripheral));
            Assert.AreEqual(1, peripheral.Counter);
        }

        [Test]
        public void ShouldUpdateSingleInterrupt()
        {
            var source = @"
sender: UnitTests.Mocks.MockIrqSender @ sysbus <0, 1>
    [Irq] -> receiver@[0]
receiver: UnitTests.Mocks.MockReceiver @ sysbus <1, 2>
sender:
    Irq -> receiver@1";

            ProcessSource(source);
            MockIrqSender sender;
            Assert.IsTrue(machine.TryGetByName("sysbus.sender", out sender));
            Assert.AreEqual(1, sender.Irq.Endpoint.Number);
        }

        [Test]
        public void ShouldCancelIrqConnection()
        {
            var source = @"
sender: UnitTests.Mocks.MockIrqSender @ sysbus <0, 1> { [Irq] -> receiver@[0] }
receiver: UnitTests.Mocks.MockReceiver @ sysbus <1, 2>
sender:
    Irq -> none";

            ProcessSource(source);
            MockIrqSender sender;
            Assert.IsTrue(machine.TryGetByName("sysbus.sender", out sender));
            Assert.IsFalse(sender.Irq.IsConnected);
        }

        [Test]
        public void ShouldUpdateDefaultIrq()
        {
            var source = @"
sender: UnitTests.Mocks.MockIrqSender @ sysbus <0, 1>
    [Irq] -> receiver@[0]
receiver: UnitTests.Mocks.MockReceiver @ sysbus <1, 2>
sender:
    -> receiver@2";

            ProcessSource(source);
            MockIrqSender sender;
            Assert.IsTrue(machine.TryGetByName("sysbus.sender", out sender));
            Assert.AreEqual(2, sender.Irq.Endpoint.Number);
        }

        [Test]
        public void ShouldUpdateMultiInterrupts()
        {
            var a = @"
sender: UnitTests.Mocks.MockGPIOByNumberConnectorPeripheral @ sysbus <0, 1>
    gpios: 64
    [0-2, 3-5, Irq, OtherIrq] -> receiver@[0-7]
    6 -> receiver2@7
receiver: UnitTests.Mocks.MockReceiver @ sysbus<1, 2>
receiver2: UnitTests.Mocks.MockReceiver @ sysbus<2, 3>";

            var source = @"
using ""A""

sender:
    [Irq, 3-4] -> receiver2@[0-2]
    6 -> receiver@7
    [7-8] -> receiver@[8-9]
";

            ProcessSource(source, a);
            MockGPIOByNumberConnectorPeripheral sender;
            MockReceiver receiver1, receiver2;
            Assert.IsTrue(machine.TryGetByName("sysbus.sender", out sender));
            Assert.IsTrue(machine.TryGetByName("sysbus.receiver", out receiver1));
            Assert.IsTrue(machine.TryGetByName("sysbus.receiver2", out receiver2));

            Assert.AreEqual(0, sender.Irq.Endpoint.Number);
            Assert.AreEqual(receiver2, sender.Irq.Endpoint.Receiver);
            Assert.AreEqual(7, sender.OtherIrq.Endpoint.Number);
            Assert.AreEqual(receiver1, sender.OtherIrq.Endpoint.Receiver);
            Assert.AreEqual(0, sender.Connections[0].Endpoint.Number);
            Assert.AreEqual(receiver1, sender.Connections[0].Endpoint.Receiver);
            Assert.AreEqual(1, sender.Connections[1].Endpoint.Number);
            Assert.AreEqual(receiver1, sender.Connections[1].Endpoint.Receiver);
            Assert.AreEqual(2, sender.Connections[2].Endpoint.Number);
            Assert.AreEqual(receiver1, sender.Connections[2].Endpoint.Receiver);
            Assert.AreEqual(1, sender.Connections[3].Endpoint.Number);
            Assert.AreEqual(receiver2, sender.Connections[3].Endpoint.Receiver);
            Assert.AreEqual(2, sender.Connections[4].Endpoint.Number);
            Assert.AreEqual(receiver2, sender.Connections[4].Endpoint.Receiver);
            Assert.AreEqual(5, sender.Connections[5].Endpoint.Number);
            Assert.AreEqual(receiver1, sender.Connections[5].Endpoint.Receiver);
            Assert.AreEqual(7, sender.Connections[6].Endpoint.Number);
            Assert.AreEqual(receiver1, sender.Connections[6].Endpoint.Receiver);
            Assert.AreEqual(8, sender.Connections[7].Endpoint.Number);
            Assert.AreEqual(receiver1, sender.Connections[7].Endpoint.Receiver);
            Assert.AreEqual(9, sender.Connections[8].Endpoint.Number);
            Assert.AreEqual(receiver1, sender.Connections[8].Endpoint.Receiver);
        }

        [Test]
        public void ShouldFailOnUsingAlreadyRegisteredPeripheralsName()
        {
            var source = @"
peripheral: UnitTests.Mocks.MockCPU";

            var peripheral = new EmptyPeripheral();
            machine.SystemBus.Register(peripheral, new Emul8.Peripherals.Bus.BusRangeRegistration(0.To(1)));
            machine.SetLocalName(peripheral, "peripheral");

            var exception = Assert.Throws<ParsingException>(() => ProcessSource(source));
            Assert.AreEqual(ParsingError.VariableAlreadyDeclared, exception.Error);
        }

        [Test]
        public void ShouldTakeAlreadyRegisteredPeripheralsAsVariables()
        {
            var source = @"
newCpu: UnitTests.Mocks.MockCPU @ sysbus 2
    OtherCpu: cpu";

            var cpu = new MockCPU(machine);
            machine.SystemBus.Register(cpu, new CPURegistrationPoint(1));
            machine.SetLocalName(cpu, "cpu");

            ProcessSource(source);
            MockCPU otherMockCpu;
            Assert.IsTrue(machine.TryGetByName("sysbus.newCpu", out otherMockCpu));
            Assert.AreEqual(cpu, otherMockCpu.OtherCpu);
        }

        [Test]
        public void ShouldReplaceInit()
        {
            var source = @"
peri: UnitTests.Mocks.EmptyPeripheral @ sysbus <0, 1>
    init:
        Increment

peri:
    init:
        Increment
        Increment";

            ProcessSource(source);
            initHandlerMock.Verify(x => x.Execute(It.IsAny<IInitable>(), new[] { "Increment", "Increment" }, It.IsAny<Action<string>>()));
        }

        [Test]
        public void ShouldAddInit()
        {
            var source = @"
peri: UnitTests.Mocks.EmptyPeripheral
    init:
        Increment

peri:
    init add:
        Increment
        Increment";


            ProcessSource(source);
            initHandlerMock.Verify(x => x.Execute(It.IsAny<IInitable>(), new[] { "Increment", "Increment", "Increment" }, It.IsAny<Action<string>>()));
        }

        [Test]
        public void ShouldUpdateSysbusInit()
        {
            var source = @"
sysbus:
    init:
        WriteByte 0 1";

            ProcessSource(source);
            initHandlerMock.Verify(x => x.Execute(It.IsAny<IInitable>(), new[] { "WriteByte 0 1" }, It.IsAny<Action<string>>()));
        }

        [Test]
        public void ShouldFailOnNotValidatedInit()
        {
            var a = @"
peri: UnitTests.Mocks.EmptyPeripheral
    init:
        Increment
        Increment";

            var source = @"
using ""A""

peri:
    init:
        Increment";
            
            var errorMessage = "Invalid init section";
            initHandlerMock.Setup(x => x.Validate(It.IsAny<IInitable>(), out errorMessage)).Returns(false);
            var exception = Assert.Throws<ParsingException>(() => ProcessSource(source, a));
            Assert.AreEqual(ParsingError.InitSectionValidationError, exception.Error);
        }

        [Test]
        public void ShouldFindCyclicDependency()
        {
            var source = @"
peri1: Emul8.UnitTests.Mocks.MockPeripheralWithDependency
    other: new Emul8.UnitTests.Mocks.MockPeripheralWithDependency
        other: peri2

peri3: Emul8.UnitTests.Mocks.MockPeripheralWithDependency
    other: peri4

peri4: Emul8.UnitTests.Mocks.MockPeripheralWithDependency
    other: peri1

peri2: Emul8.UnitTests.Mocks.MockPeripheralWithDependency
    other: peri3
";

            var exception = Assert.Throws<ParsingException>(() => ProcessSource(source));
            Assert.AreEqual(ParsingError.CreationOrderCycle, exception.Error);
        }

        [Test]
        public void ShouldFindCyclicReferenceToItself()
        {
            var source = @"
peri: Emul8.UnitTests.Mocks.MockPeripheralWithDependency
    other: peri";

            var exception = Assert.Throws<ParsingException>(() => ProcessSource(source));
            Assert.AreEqual(ParsingError.CreationOrderCycle, exception.Error);
        }

        [Test]
        public void ShouldFindCyclicReferenceBetweenFiles()
        {
            var a = @"
peri1: Emul8.UnitTests.Mocks.MockPeripheralWithDependency
    other: peri3
peri3: Emul8.UnitTests.Mocks.MockPeripheralWithDependency
";

            var source = @"
using ""A""
peri2: Emul8.UnitTests.Mocks.MockPeripheralWithDependency
    other: peri1

peri3:
    other: peri2";

            var exception = Assert.Throws<ParsingException>(() => ProcessSource(source, a));
            Assert.AreEqual(ParsingError.CreationOrderCycle, exception.Error);
        }

        [Test]
        public void ShouldSetPropertyOfInlineObject()
        {
            var source = @"
cpu: UnitTests.Mocks.MockCPU @ sysbus
    OtherCpu: new UnitTests.Mocks.MockCPU
        OtherCpu: new UnitTests.Mocks.MockCPU
            Placeholder: ""something""";

            ProcessSource(source);
            MockCPU cpu;
            Assert.IsTrue(machine.TryGetByName("sysbus.cpu", out cpu));
            Assert.AreEqual("something", ((cpu.OtherCpu as MockCPU).OtherCpu as MockCPU).Placeholder);
        }

        [Test]
        public void ShouldRegisterPeripheralWithManyRegistrationPoints()
        {
            var source = @"
peripheral: UnitTests.Mocks.EmptyPeripheral @{
    sysbus <0x100, +0x10>;
    sysbus <0x200, +0x20>} as ""alias""
";
            ProcessSource(source);
            EmptyPeripheral peripheral;
            Assert.IsTrue(machine.TryGetByName("sysbus.alias", out peripheral));
            var ranges = machine.GetPeripheralRegistrationPoints(machine.SystemBus, peripheral).OfType<Emul8.Peripherals.Bus.BusRangeRegistration>().Select(x => x.Range).ToArray();
            Assert.AreEqual(0x100.By(0x10), ranges[0]);
            Assert.AreEqual(0x200.By(0x20), ranges[1]);
            Assert.AreEqual(2, ranges.Length);
        }

        [Test]
        public void ShouldFailOnNonExistingReferenceInCtorAttribute()
        {
            var source = @"
peripheral: Emul8.UnitTests.Mocks.MockPeripheralWithDependency
    other: nonExististing";

            var exception = Assert.Throws<ParsingException>(() => ProcessSource(source));
            Assert.AreEqual(ParsingError.MissingReference, exception.Error);
        }

        [Test]
        public void ShouldProcessEntryDependantOnSysbus()
        {
            var source = @"
peripheral: Emul8.UnitTests.Mocks.MockPeripheralWithDependency
    other: sysbus";

            ProcessSource(source);
        }

        [Test]
        public void ShouldProcessEntryDependantOnSysbusWithUpdateEntry()
        {
            var source = @"
sysbus:
    init:
        Method

peripheral: Emul8.UnitTests.Mocks.MockPeripheralWithDependency
    other: sysbus";

            ProcessSource(source);
        }

        [Test]
        public void ShouldCatchExceptionOnEntryConstruction()
        {
            var source = @"
peripheral: Emul8.UnitTests.Mocks.MockPeripheralWithDependency
    throwException: true";

            var exception = Assert.Throws<ParsingException>(() => ProcessSource(source));
            Assert.AreEqual(ParsingError.ConstructionException, exception.Error);
        }

        [Test]
        public void ShouldCatchExceptionOnObjectValueConstruction()
        {
            var source = @"
peripheral: Emul8.UnitTests.Mocks.MockPeripheralWithDependency
    other: new Emul8.UnitTests.Mocks.MockPeripheralWithDependency
        throwException: true";

            var exception = Assert.Throws<ParsingException>(() => ProcessSource(source));
            Assert.AreEqual(ParsingError.ConstructionException, exception.Error);
        }

        [Test]
        public void ShouldCatchExceptionOnPropertySetting()
        {
            var source = @"
peripheral: UnitTests.Mocks.EmptyPeripheral
    ThrowingProperty: 1";

            var exception = Assert.Throws<ParsingException>(() => ProcessSource(source));
            Assert.AreEqual(ParsingError.PropertySettingException, exception.Error);
        }

        [Test]
        public void ShouldCatchExceptionOnRegistration()
        {
            var source = @"
peripheral: UnitTests.Mocks.EmptyPeripheral @ {
    sysbus <0x100, +0x100>;
    sysbus <0x150, +0x100>    
}";

            var exception = Assert.Throws<ParsingException>(() => ProcessSource(source));
            Assert.AreEqual(ParsingError.RegistrationException, exception.Error);
        }

        [Test]
        public void ShouldHandleNameConflict()
        {
            var source = @"
p1: UnitTests.Mocks.EmptyPeripheral @ sysbus <0x100, +0x50>
p2: UnitTests.Mocks.EmptyPeripheral @ sysbus <0x200, +0x50> as ""p1""
";

            var exception = Assert.Throws<ParsingException>(() => ProcessSource(source));
            Assert.AreEqual(ParsingError.NameSettingException, exception.Error);
        }

        [Test]
        public void ShouldProcessRepeatedRegistration()
        {
            var source = @"
mockRegister1: Emul8.UnitTests.Mocks.MockRegister @ sysbus 0x0
cpu: UnitTests.Mocks.MockCPU @ {
    mockRegister1;
    mockRegister2
}
mockRegister2: Emul8.UnitTests.Mocks.MockRegister @ sysbus 0x100
";
            ProcessSource(source);

            ICPU peripheral;
            Assert.IsTrue(machine.TryGetByName("sysbus.mockRegister1.cpu", out peripheral));
            Assert.IsTrue(machine.TryGetByName("sysbus.mockRegister2.cpu", out peripheral));
        }

        [TestFixtureSetUp]
        public void Init()
        {
            string emul8Dir;
            if(!Misc.TryGetEmul8Directory(out emul8Dir))
            {
                throw new ArgumentException("Couldn't get Emul8 directory.");
            }
            TypeManager.Instance.Scan(emul8Dir);
        }

        [SetUp]
        public void SetUp()
        {
            machine = new Machine();
            EmulationManager.Instance.CurrentEmulation.AddMachine(machine, "machine");
            initHandlerMock = new Mock<IInitHandler>();
            string nullMessage = null;
            initHandlerMock.Setup(x => x.Validate(It.IsAny<IInitable>(), out nullMessage)).Returns(true);
        }

        [TearDown]
        public void TearDown()
        {
            var currentEmulation = EmulationManager.Instance.CurrentEmulation;
            currentEmulation.RemoveMachine(machine);
        }

        private void ProcessSource(params string[] sources)
        {
            var letters = Enumerable.Range(0, sources.Length - 1).Select(x => (char)('A' + x)).ToArray();
            var usingResolver = new FakeUsingResolver();
            for(var i = 1; i < sources.Length; i++)
            {
                usingResolver.With(letters[i - 1].ToString(), sources[i]);
            }
            var creationDriver = new CreationDriver(machine, usingResolver, initHandlerMock.Object);
            creationDriver.ProcessDescription(sources[0]);
        }

        private Mock<IInitHandler> initHandlerMock;
        private Machine machine;
    }
}
