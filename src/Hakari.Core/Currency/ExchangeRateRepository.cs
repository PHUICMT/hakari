using System.Globalization;
using Hakari.Core.Indexing;

namespace Hakari.Core.Currency;

public sealed class ExchangeRateRepository(IndexStore store)
{
    private const string DayFormat = "yyyy-MM-dd";

    private const string SelectSql = """
        SELECT day, units_per_dollar, source
        FROM exchange_rates
        WHERE currency = $currency
        ORDER BY day
        """;

    private const string UpsertSql = """
        INSERT INTO exchange_rates (currency, day, units_per_dollar, source)
        VALUES ($currency, $day, $unitsPerDollar, $source)
        ON CONFLICT (currency, day) DO UPDATE SET
            units_per_dollar = excluded.units_per_dollar,
            source = excluded.source
        """;

    public IReadOnlyList<ExchangeRate> Load(string currency)
    {
        using var command = store.Connection.CreateCommand();
        command.CommandText = SelectSql;
        command.Parameters.AddWithValue("$currency", currency);

        var rates = new List<ExchangeRate>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var day = DateOnly.ParseExact(
                reader.GetString(0),
                DayFormat,
                CultureInfo.InvariantCulture);
            rates.Add(new ExchangeRate(day, currency, reader.GetDecimal(1), reader.GetString(2)));
        }

        return rates;
    }

    public void Save(IEnumerable<ExchangeRate> rates)
    {
        using var transaction = store.Connection.BeginTransaction();
        foreach (var rate in rates)
        {
            using var command = store.Connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = UpsertSql;
            command.Parameters.AddWithValue("$currency", rate.Currency);
            command.Parameters.AddWithValue(
                "$day",
                rate.Day.ToString(DayFormat, CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("$unitsPerDollar", rate.UnitsPerDollar);
            command.Parameters.AddWithValue("$source", rate.Source);
            command.ExecuteNonQuery();
        }

        transaction.Commit();
    }
}
