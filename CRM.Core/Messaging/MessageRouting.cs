namespace CRM.Core.Messaging;

public static class MessageRouting
{
    /// <summary>
    /// Default routing key for a message type, e.g. <c>CustomerCreated</c> → <c>customer.created</c>.
    /// </summary>
    public static string For<T>() => For(typeof(T));

    public static string For(Type messageType)
    {
        var name = messageType.Name;
        var builder = new System.Text.StringBuilder(name.Length + 4);

        for(var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if(char.IsUpper(c) && i > 0)
            {
                builder.Append('.');
            }
            builder.Append(char.ToLowerInvariant(c));
        }

        return builder.ToString();
    }
}
