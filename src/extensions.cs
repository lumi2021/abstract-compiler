namespace Compiler;

public static class Extensions
{

    public static void Push<T>(this List<T> list, T item)
    {
        list.Insert(0, item);
    }
    public static void PushRange<T>(this List<T> list, params T[] items)
    {
        list.InsertRange(0, items);
    }

    public static T Pop<T>(this List<T> list)
    {
        var item = list[^1];
        list.RemoveAt(0);
        return item;
    }

}