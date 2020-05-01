using System.Globalization;

namespace Unity.Tiny.Animation.Editor
{
    readonly struct BindingInfo
    {
        public readonly bool Success;
        public readonly ulong StableTypeHash;
        public readonly ushort FieldOffset;
        public readonly ushort FieldSize;

        public static BindingInfo UnsuccessfulBinding => new BindingInfo(false, 0, 0, 0);

        public BindingInfo(bool success, ulong stableTypeHash, ushort fieldOffset, ushort fieldSize)
        {
            Success = success;
            StableTypeHash = stableTypeHash;
            FieldOffset = fieldOffset;
            FieldSize = fieldSize;
        }

        public override string ToString()
        {
            return Success
                ? "Successful binding - " +
                $"{nameof(StableTypeHash)} = {StableTypeHash.ToString(NumberFormatInfo.InvariantInfo)} - " +
                $"{nameof(FieldOffset)} = {FieldOffset.ToString(NumberFormatInfo.InvariantInfo)} - " +
                $"{nameof(FieldSize)} = {FieldSize.ToString(NumberFormatInfo.InvariantInfo)}"
                : "Binding unsuccessful";
        }
    }
}
