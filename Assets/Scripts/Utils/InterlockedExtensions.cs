namespace Utils
{
    public static class InterlockedExtensions
    {
        public static void InterlockedAdd(this ref float location, float value)
        {
            var newCurrentValue = location;
            while (true)
            {
                var currentValue = newCurrentValue;
                var newValue = currentValue + value;
                newCurrentValue = System.Threading.Interlocked.CompareExchange(ref location, newValue, currentValue);
                if (newCurrentValue.Equals(currentValue)) return;
            }
        }
    }
}