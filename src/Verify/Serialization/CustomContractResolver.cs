﻿class CustomContractResolver :
    DefaultContractResolver
{
    SerializationSettings settings;

    public CustomContractResolver(SerializationSettings settings) =>
        this.settings = settings;

    protected override JsonDictionaryContract CreateDictionaryContract(Type objectType)
    {
        var contract = base.CreateDictionaryContract(objectType);
        contract.DictionaryKeyResolver = value => ResolveDictionaryKey(contract, value);
        if (!typeof(IDictionaryWrapper).IsAssignableFrom(objectType))
        {
            contract.OrderByKey = true;
        }
        contract.ShouldSerializeItem = (key,value) =>
        {
            if(key is string stringKey &&
               settings.TryGetScrubOrIgnoreByName(stringKey, out var scrubOrIgnore))
            {
                if (scrubOrIgnore == ScrubOrIgnore.Ignore)
                {
                    return false;
                }
            }

            return true;
        };
        return contract;
    }

    string ResolveDictionaryKey(JsonDictionaryContract contract, string value)
    {
        var counter = Counter.Current;
        var keyType = contract.DictionaryKeyType;
        if (keyType == typeof(Guid))
        {
            if (settings.TryParseConvertGuid(counter, value, out var result))
            {
                return result;
            }
        }

        if (keyType == typeof(DateTimeOffset))
        {
            if (settings.TryParseConvertDateTimeOffset(counter, value, out var result))
            {
                return result;
            }
        }

        if (keyType == typeof(DateTime))
        {
            if (settings.TryParseConvertDateTime(counter, value, out var result))
            {
                return result;
            }
        }

        if (keyType == typeof(Type))
        {
            var type = Type.GetType(value);
            if (type is null)
            {
                throw new($"Could not load type `{value}`.");
            }

            return type.SimpleName();
        }

        return value;
    }

    static FieldInfo exceptionMessageField = typeof(Exception).GetField("_message", BindingFlags.Instance | BindingFlags.NonPublic)!;

    protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
    {
        var properties = base.CreateProperties(type, memberSerialization);
        if (type.IsException())
        {
            var stackTrace = properties.Single(_ => _.PropertyName == "StackTrace");
            properties.Remove(stackTrace);
            properties.Add(stackTrace);
            properties.Insert(0,
                new(typeof(string), typeof(Exception))
                {
                    PropertyName = "Type",
                    ValueProvider = new TypeNameProvider(type),
                    Ignored = false,
                    Readable = true,
                    Writable = false
                });
        }

        if (VerifierSettings.sortPropertiesAlphabetically)
        {
            properties = properties
                // Still honor explicit ordering
                .OrderBy(p => p.Order ?? -1)
                .ThenBy(p => p.PropertyName, StringComparer.Ordinal)
                .ToList();
        }

        return properties;
    }

    protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization serialization)
    {
        var property = base.CreateProperty(member, serialization);

        var valueProvider = property.ValueProvider;
        var memberType = property.PropertyType;
        if (memberType == null || valueProvider is null)
        {
            return property;
        }

        if (settings.TryGetScrubOrIgnore(member, out var scrubOrIgnore))
        {
            if (scrubOrIgnore == ScrubOrIgnore.Ignore)
            {
                property.Ignored = true;
            }
            else
            {
                property.PropertyType = typeof(string);
                property.ValueProvider = new ScrubbedProvider();
            }

            return property;
        }

        if (member.Name == "Message")
        {
            if (member.DeclaringType == typeof(ArgumentException))
            {
                valueProvider = new ExpressionValueProvider(exceptionMessageField);
            }
        }

        if (memberType.IsException())
        {
            property.TypeNameHandling = TypeNameHandling.All;
        }

        if (property.PropertyType == typeof(bool))
        {
            property.DefaultValueHandling = DefaultValueHandling.Include;
        }

        property.ValueProvider = new CustomValueProvider(
            valueProvider,
            memberType,
            exception => settings.ShouldIgnoreException(exception),
            VerifierSettings.GetMemberConverter(member),
            settings);

        return property;
    }

    protected override JsonArrayContract CreateArrayContract(Type objectType)
    {
        var contract = base.CreateArrayContract(objectType);
        contract.ShouldSerializeItem = item =>
        {
            if (item != null &&
                settings.TryGetScrubOrIgnoreByInstance(item, out var scrubOrIgnore))
            {
                if (scrubOrIgnore == ScrubOrIgnore.Ignore)
                {
                    return false;
                }
            }

            return true;
        };

        return contract;
    }
}