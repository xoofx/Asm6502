// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace AsmMos6502;

public readonly struct Mos6502Instruction : IEquatable<Mos6502Instruction>, ISpanFormattable
{
    private readonly uint _raw;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Mos6502Instruction(uint raw) => _raw = raw;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Mos6502Instruction(Mos6502OpCode opCode)
    {
        _raw = ((uint)opCode);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Mos6502Instruction(Mos6502OpCode opCode, sbyte lowByte) : this(opCode, (byte)lowByte)
    {
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Mos6502Instruction(Mos6502OpCode opCode, byte lowByte)
    {
        _raw = ((uint)opCode | ((uint)lowByte << 8));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Mos6502Instruction(Mos6502OpCode opCode, ushort value)
    {
        _raw = ((uint)opCode | ((uint)value << 8));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Mos6502Instruction(Mos6502OpCode opCode, byte lowByte, byte highByte)
    {
        _raw = ((uint)opCode | ((uint)lowByte << 8) | ((uint)highByte<< 16));
    }

    public Mos6502OpCode OpCode => (Mos6502OpCode)(byte)_raw;

    public Mos6502Mnemonic Mnemonic => OpCode.ToMnemonic();

    public Mos6502AddressingMode AddressingMode => OpCode.ToAddressingMode();

    public bool IsValid => AddressingMode != Mos6502AddressingMode.Unknown;

    public int CycleCount => OpCode.ToCycleCount();

    public int SizeInBytes => AddressingMode.ToSizeInBytes();

    public ushort Operand => (ushort)(_raw >> 8);
    
    public byte LowOperand => (byte)(_raw >> 8);

    public byte HighOperand => (byte)(_raw >> 16);

    internal Mos6502AddressKind GetAddressKind()
    {
        return AddressingMode switch
        {
            Mos6502AddressingMode.Absolute or
            Mos6502AddressingMode.AbsoluteX or
            Mos6502AddressingMode.AbsoluteY => Mos6502AddressKind.Absolute,
            Mos6502AddressingMode.Relative => Mos6502AddressKind.Relative,
            _ => Mos6502AddressKind.None
        };
    }
    
    [UnscopedRef]
    public ReadOnlySpan<byte> AsSpan => MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<uint, byte>(ref Unsafe.AsRef(in _raw)), SizeInBytes);

    public bool Equals(Mos6502Instruction other) => _raw == other._raw;

    public override bool Equals(object? obj) => obj is Mos6502Instruction other && Equals(other);

    public override int GetHashCode() => (int)_raw;
    
    public static Mos6502Instruction Decode(ReadOnlySpan<byte> buffer) => TryDecode(buffer, out var instruction, out var count) ? instruction : default;

    public static bool TryDecode(ReadOnlySpan<byte> buffer, out Mos6502Instruction instruction, out int sizeInBytes)
    {
        instruction = default;
        sizeInBytes = 0;

        if (buffer.Length < 1)
            return false;
        var opCode = buffer[0];

        var addressingMode = Mos6502Tables.GetAddressingModeFromOpcode(opCode);
        if (addressingMode == Mos6502AddressingMode.Unknown)
            return false;

        sizeInBytes = Mos6502Tables.GetSizeInBytesFromAddressingMode(addressingMode);

        if (buffer.Length < sizeInBytes)
            return false;

        switch (sizeInBytes)
        {
            case 1:
                instruction = new Mos6502Instruction((Mos6502OpCode)opCode);
                break;
            case 2:
                instruction = new Mos6502Instruction((Mos6502OpCode)opCode, buffer[1]);
                break;
            default:
                instruction = new Mos6502Instruction((Mos6502OpCode)opCode, buffer[1], buffer[2]);
                break;
        }

        return true;
    }

    public static bool operator ==(Mos6502Instruction left, Mos6502Instruction right) => left.Equals(right);

    public static bool operator !=(Mos6502Instruction left, Mos6502Instruction right) => !left.Equals(right);

    public string ToString(string? format, IFormatProvider? formatProvider)
    {
        Span<char> destination = stackalloc char[64]; // Allocate a temporary buffer
        if (!TryFormat(destination, out var charsWritten, format, formatProvider))
        {
            var buffer = ArrayPool<char>.Shared.Rent(1024);
            destination = buffer;
            if (!TryFormat(destination, out charsWritten, format, formatProvider))
            {
                throw new InvalidOperationException("Failed to format Mos6502Instruction.");
            }
        }

        return destination.Slice(0, charsWritten).ToString();
    }

    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
        => TryFormat(destination, out charsWritten, format, provider, null);

    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider, Mos6502TryFormatDelegate? tryFormatDelegate)
    {
        var mnemonic = Mnemonic;
        bool lowercase = format.Length == 1 && format[0] == 'l';
        var mnemonicText = mnemonic.ToText(lowercase);
        var addressingMode = AddressingMode;
        switch (addressingMode)
        {
            case Mos6502AddressingMode.Unknown:
                return destination.TryWrite(provider, $"???", out charsWritten);
            case Mos6502AddressingMode.Absolute:
            {
                if (tryFormatDelegate != null)
                {
                    if (destination.TryWrite(provider, $"{mnemonicText} ", out var charsWrittenTemp) && tryFormatDelegate(Operand, destination.Slice(charsWrittenTemp), out var charsWrittenTryFormat))
                    {
                        charsWritten = charsWrittenTemp + charsWrittenTryFormat;
                        return true;
                    }
                }

                return destination.TryWrite(provider, $"{mnemonicText} ${Operand:X4}", out charsWritten);
            }
            case Mos6502AddressingMode.AbsoluteX:
            {
                if (tryFormatDelegate != null)
                {
                    if (destination.TryWrite(provider, $"{mnemonicText} ", out var charsWrittenTemp) && tryFormatDelegate(Operand, destination.Slice(charsWrittenTemp), out var charsWrittenTryFormat) && (lowercase
                            ? destination.Slice(charsWrittenTemp + charsWrittenTryFormat).TryWrite(provider, $",x", out var charsWrittenTempX)
                            : destination.Slice(charsWrittenTemp + charsWrittenTryFormat).TryWrite(provider, $",X", out charsWrittenTempX)))
                    {
                        charsWritten = charsWrittenTemp + charsWrittenTryFormat + charsWrittenTempX;
                        return true;
                    }
                }

                return lowercase ? destination.TryWrite(provider, $"{mnemonicText} ${Operand:X4},x", out charsWritten) : destination.TryWrite(provider, $"{mnemonicText} ${Operand:X4},X", out charsWritten);
            }
            case Mos6502AddressingMode.AbsoluteY:
            {
                if (tryFormatDelegate != null)
                {
                    if (destination.TryWrite(provider, $"{mnemonicText} ", out var charsWrittenTemp) && tryFormatDelegate(Operand, destination.Slice(charsWrittenTemp), out var charsWrittenTryFormat) && (lowercase
                            ? destination.Slice(charsWrittenTemp + charsWrittenTryFormat).TryWrite(provider, $",y", out var charsWrittenTempY)
                            : destination.Slice(charsWrittenTemp + charsWrittenTryFormat).TryWrite(provider, $",Y", out charsWrittenTempY)))
                    {
                        charsWritten = charsWrittenTemp + charsWrittenTryFormat + charsWrittenTempY;
                        return true;
                    }
                }

                return lowercase ? destination.TryWrite(provider, $"{mnemonicText} ${Operand:X4},y", out charsWritten) : destination.TryWrite(provider, $"{mnemonicText} ${Operand:X4},Y", out charsWritten);
            }
            case Mos6502AddressingMode.Accumulator:
                return lowercase ? destination.TryWrite(provider, $"{mnemonicText} a", out charsWritten) : destination.TryWrite(provider, $"{mnemonicText} A", out charsWritten);
            case Mos6502AddressingMode.Immediate:
                return destination.TryWrite(provider, $"{mnemonicText} #${Operand:X2}", out charsWritten);
            case Mos6502AddressingMode.Implied:
                return destination.TryWrite(provider, $"{mnemonicText}", out charsWritten);
            case Mos6502AddressingMode.Indirect:
                return destination.TryWrite(provider, $"{mnemonicText} (${Operand:X4})", out charsWritten);
            case Mos6502AddressingMode.IndirectX:
                return lowercase ? destination.TryWrite(provider, $"{mnemonicText} (${LowOperand:X2},x)", out charsWritten) : destination.TryWrite(provider, $"{mnemonicText} (${LowOperand:X2},X)", out charsWritten);
            case Mos6502AddressingMode.IndirectY:
                return lowercase ? destination.TryWrite(provider, $"{mnemonicText} (${LowOperand:X2}),y", out charsWritten) : destination.TryWrite(provider, $"{mnemonicText} (${LowOperand:X2}),Y", out charsWritten);
            case Mos6502AddressingMode.Relative:
            {
                if (tryFormatDelegate != null)
                {
                    if (destination.TryWrite(provider, $"{mnemonicText} ", out var charsWrittenTemp) && tryFormatDelegate((ushort)(short)(sbyte)LowOperand, destination.Slice(charsWrittenTemp), out var charsWrittenTryFormat))
                    {
                        charsWritten = charsWrittenTemp + charsWrittenTryFormat;
                        return true;
                    }
                }

                return destination.TryWrite(provider, $"{mnemonicText} ${(sbyte)LowOperand}", out charsWritten);
            }
            case Mos6502AddressingMode.ZeroPage:
                return destination.TryWrite(provider, $"{mnemonicText} ${LowOperand:X2}", out charsWritten);
            case Mos6502AddressingMode.ZeroPageX:
                return lowercase ? destination.TryWrite(provider, $"{mnemonicText} ${LowOperand:X2},x", out charsWritten) : destination.TryWrite(provider, $"{mnemonicText} ${LowOperand:X2},X", out charsWritten);
            case Mos6502AddressingMode.ZeroPageY:
                return lowercase ? destination.TryWrite(provider, $"{mnemonicText} ${LowOperand:X2},y", out charsWritten) : destination.TryWrite(provider, $"{mnemonicText} ${LowOperand:X2},Y", out charsWritten);
            default:
                throw new InvalidOperationException(); // Should never happen
        }
    }

    public override string ToString()
    {
        return
            $"{nameof(_raw)}: {_raw}, {nameof(OpCode)}: {OpCode}, {nameof(Mnemonic)}: {Mnemonic}, {nameof(AddressingMode)}: {AddressingMode}, {nameof(SizeInBytes)}: {SizeInBytes}, {nameof(Operand)}: {Operand}, {nameof(LowOperand)}: {LowOperand}, {nameof(HighOperand)}: {HighOperand}";
    }
}
