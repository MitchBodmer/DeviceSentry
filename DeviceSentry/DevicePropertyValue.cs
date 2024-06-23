namespace DeviceSentry;

internal record DevicePropertyValue
{
    private readonly string? _value;

    public DevicePropertyValue(object value)
    {
        if (value is byte[] bytes)
        {
            _value = string.Join(string.Empty, bytes.Select(@byte => @byte.ToString("X")));
        }
        else
        {
            _value = value.ToString();
        }
    }

    public override string? ToString() => _value;

    public static implicit operator string?(DevicePropertyValue devicePropertyValue) =>
        devicePropertyValue.ToString();
}