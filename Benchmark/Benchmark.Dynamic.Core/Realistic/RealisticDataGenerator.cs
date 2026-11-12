namespace Benchmark.Dynamic.Core.Realistic;

public enum RealisticDistribution {
    UniformWithDuplicates,
    SkewedZipf,
    HighCardinality,
    LowCardinality,
}

public static class RealisticDataGenerator {
    private static readonly string[] SampleSymbols = [
        "AAPL", "MSFT", "GOOGL", "AMZN", "NVDA", "META", "TSLA", "BRK.B",
        "JPM", "JNJ", "V", "PG", "UNH", "HD", "MA", "XOM"
    ];

    private static readonly string[] SampleSectors = [
        "Technology", "Financials", "Healthcare", "ConsumerDiscretionary",
        "CommunicationServices", "Industrials", "ConsumerStaples", "Energy"
    ];

    private static readonly string[] SampleCities = [
        "Tokyo", "New York", "London", "Paris", "Berlin",
        "Singapore", "Sydney", "Toronto", "Chicago", "San Francisco"
    ];

    private static readonly string?[] SampleCoupons = [
        null, null, "SUMMER10", "WELCOME20", "VIP50", null, "SAVE5", null
    ];

    public static Trade[] GenerateTrades(int count, Seed256 seed, RealisticDistribution distribution = RealisticDistribution.UniformWithDuplicates) {
        if (count == 0) return [];
        var prng = new DeterministicPrng(seed);
        var result = new Trade[count];
        var symbolCount = GetDistinctKeyCount(count, distribution, SampleSymbols.Length);
        var accountCount = Math.Max(1, GetDistinctKeyCount(count, distribution, count / 2));

        for (int i = 0; i < count; i++) {
            var symIdx = SelectIndex(prng, symbolCount, distribution) % SampleSymbols.Length;
            var accId = SelectIndex(prng, accountCount, distribution) + 1;
            var qty = prng.NextInt32(1000) + 1;
            var price = Math.Round((decimal)(prng.NextUInt64() % 50000 + 100) / 100m, 2);
            var status = (TradeStatus)(prng.NextInt32(4));
            var isCash = (prng.NextUInt64() & 1) == 1;
            var ticks = 638000000000000000L + (long)(prng.NextUInt64() % 10000000000L);

            result[i] = new Trade(i + 1, accId, SampleSymbols[symIdx], qty, price, status, ticks, isCash);
        }
        return result;
    }

    public static Account[] GenerateAccounts(int count, Seed256 seed, RealisticDistribution distribution = RealisticDistribution.UniformWithDuplicates) {
        if (count == 0) return [];
        var prng = new DeterministicPrng(seed);
        var result = new Account[count];
        var brokerCount = Math.Max(1, GetDistinctKeyCount(count, distribution, 16));

        for (int i = 0; i < count; i++) {
            var custId = (i / 2) + 1;
            var brokerId = SelectIndex(prng, brokerCount, distribution) + 1;
            var balance = Math.Round((decimal)(prng.NextUInt64() % 10000000) / 100m, 2);
            var tier = (prng.NextInt32(3)) + 1;
            var name = $"Account-{i + 1}";

            result[i] = new Account(i + 1, custId, brokerId, name, balance, tier);
        }
        return result;
    }

    public static Security[] GenerateSecurities(int count, Seed256 seed, RealisticDistribution distribution = RealisticDistribution.UniformWithDuplicates) {
        if (count == 0) return [];
        var prng = new DeterministicPrng(seed);
        var actualCount = Math.Min(count, SampleSymbols.Length);
        var result = new Security[actualCount];

        for (int i = 0; i < actualCount; i++) {
            var symbol = SampleSymbols[i];
            var sector = SampleSectors[i % SampleSectors.Length];
            var compId = (i + 1) * 10;
            var lastPrice = Math.Round((decimal)(prng.NextUInt64() % 50000 + 500) / 100m, 2);
            result[i] = new Security(symbol, sector, compId, lastPrice);
        }
        return result;
    }

    public static Customer[] GenerateCustomers(int count, Seed256 seed, RealisticDistribution distribution = RealisticDistribution.UniformWithDuplicates) {
        if (count == 0) return [];
        var prng = new DeterministicPrng(seed);
        var result = new Customer[count];

        for (int i = 0; i < count; i++) {
            var cityIdx = SelectIndex(prng, SampleCities.Length, distribution);
            var age = prng.NextInt32(65) + 18;
            var isPremium = (prng.NextUInt64() % 5) == 0; // 20% premium
            result[i] = new Customer {
                Id = i + 1,
                Name = $"Customer-{i + 1}",
                City = SampleCities[cityIdx % SampleCities.Length],
                Age = age,
                IsPremium = isPremium
            };
        }
        return result;
    }

    public static SalesOrder[] GenerateSalesOrders(int count, Seed256 seed, RealisticDistribution distribution = RealisticDistribution.UniformWithDuplicates) {
        if (count == 0) return [];
        var prng = new DeterministicPrng(seed);
        var result = new SalesOrder[count];
        var custCount = Math.Max(1, GetDistinctKeyCount(count, distribution, count / 3));

        for (int i = 0; i < count; i++) {
            var custId = SelectIndex(prng, custCount, distribution) + 1;
            var amount = Math.Round((decimal)(prng.NextUInt64() % 100000 + 50) / 100m, 2);
            var status = (OrderStatus)(prng.NextInt32(5));
            var coupon = SampleCoupons[prng.NextInt32(SampleCoupons.Length)];
            var ticks = 638000000000000000L + (long)(prng.NextUInt64() % 10000000000L);

            result[i] = new SalesOrder {
                Id = i + 1,
                CustomerId = custId,
                Amount = amount,
                OrderedAtTicks = ticks,
                Status = status,
                CouponCode = coupon
            };
        }
        return result;
    }

    public static int[] GenerateInts(int count, Seed256 seed, RealisticDistribution distribution = RealisticDistribution.UniformWithDuplicates) {
        if (count == 0) return [];
        var prng = new DeterministicPrng(seed);
        var result = new int[count];
        var distinctCount = Math.Max(1, GetDistinctKeyCount(count, distribution, count / 4));

        for (int i = 0; i < count; i++) {
            var v = SelectIndex(prng, distinctCount, distribution);
            result[i] = v;
        }
        return result;
    }

    private static int GetDistinctKeyCount(int totalCount, RealisticDistribution dist, int fallbackMax) {
        return dist switch {
            RealisticDistribution.HighCardinality => Math.Max(1, (int)(totalCount * 0.95)),
            RealisticDistribution.LowCardinality => Math.Min(8, Math.Max(2, totalCount / 4)),
            RealisticDistribution.UniformWithDuplicates => Math.Max(1, Math.Min(fallbackMax, totalCount / 4)),
            RealisticDistribution.SkewedZipf => Math.Max(1, Math.Min(fallbackMax, totalCount / 5)),
            _ => Math.Max(1, fallbackMax)
        };
    }

    private static int SelectIndex(DeterministicPrng prng, int cardinality, RealisticDistribution dist) {
        if (cardinality <= 1) return 0;
        if (dist == RealisticDistribution.SkewedZipf) {
            // 80% の確率で上位 10% のキーを選択 (Zipf 類似の冪乗分布)
            if (prng.NextInt32(100) < 80) {
                var topK = Math.Max(1, cardinality / 10);
                return prng.NextInt32(topK);
            }
        }
        return prng.NextInt32(cardinality);
    }
}
