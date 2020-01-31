using System.Globalization;

namespace Unity.Tiny.Animation.Editor
{
    readonly struct BindingInfo
    {
        public readonly bool success;
        public readonly ulong stableTypeHash;
        public readonly ushort fieldOffset;
        public readonly ushort fieldSize;

        public static BindingInfo UnsuccessfulBinding => new BindingInfo(false, 0, 0, 0);

        public BindingInfo(bool success, ulong stableTypeHash, ushort fieldOffset, ushort fieldSize)
        {
            this.success = success;
            this.stableTypeHash = stableTypeHash;
            this.fieldOffset = fieldOffset;
            this.fieldSize = fieldSize;
        }

        public override string ToString()
        {
            return success
                ? "Successful binding - " +
                  $"{nameof(stableTypeHash)} = {stableTypeHash.ToString(NumberFormatInfo.InvariantInfo)} - " +
                  $"{nameof(fieldOffset)} = {fieldOffset.ToString(NumberFormatInfo.InvariantInfo)} - " +
                  $"{nameof(fieldSize)} = {fieldSize.ToString(NumberFormatInfo.InvariantInfo)}"
                : "Binding unsuccessful";
        }
    }
}
