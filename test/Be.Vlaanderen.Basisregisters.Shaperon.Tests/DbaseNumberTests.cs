namespace Be.Vlaanderen.Basisregisters.Shaperon
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using Albedo;
    using AutoFixture;
    using AutoFixture.Idioms;
    using Xunit;

    public class DbaseNumberTests
    {
        private readonly Fixture _fixture;

        public DbaseNumberTests()
        {
            _fixture = new Fixture();
            _fixture.CustomizeDbaseFieldName();
            _fixture.CustomizeDbaseFieldLength(DbaseNumber.MaximumLength);
            _fixture.CustomizeDbaseDecimalCount(DbaseNumber.MaximumDecimalCount);
            _fixture.CustomizeDbaseDouble();
            _fixture.Register(() => new BinaryReader(new MemoryStream()));
            _fixture.Register(() => new BinaryWriter(new MemoryStream()));
        }

        [Fact]
        public void MaximumDecimalCountReturnsExpectedValue()
        {
            Assert.Equal(new DbaseDecimalCount(15), DbaseNumber.MaximumDecimalCount);
        }

        [Fact]
        public void MaximumIntegerDigitsReturnsExpectedValue()
        {
            Assert.Equal(new DbaseIntegerDigits(18), DbaseNumber.MaximumIntegerDigits);
        }

        [Fact]
        public void MaximumLengthReturnsExpectedValue()
        {
            Assert.Equal(new DbaseFieldLength(18), DbaseNumber.MaximumLength);
        }

        [Fact]
        public void PositiveValueMinimumLengthReturnsExpectedValue()
        {
            Assert.Equal(new DbaseFieldLength(3), DbaseNumber.PositiveValueMinimumLength);
        }

        [Fact]
        public void NegativeValueMinimumLengthReturnsExpectedValue()
        {
            Assert.Equal(new DbaseFieldLength(4), DbaseNumber.NegativeValueMinimumLength);
        }

        [Fact]
        public void CreateFailsIfFieldIsNull()
        {
            Assert.Throws<ArgumentNullException>(
                () => new DbaseNumber(null)
            );
        }

        [Fact]
        public void CreateFailsIfFieldIsNotNumber()
        {
            var fieldType = new Generator<DbaseFieldType>(_fixture)
                .First(specimen => specimen != DbaseFieldType.Number);
            var length = _fixture.GenerateDbaseDoubleLength();
            var decimalCount = _fixture.GenerateDbaseDoubleDecimalCount(length);
            Assert.Throws<ArgumentException>(
                () =>
                    new DbaseNumber(
                        new DbaseField(
                            _fixture.Create<DbaseFieldName>(),
                            fieldType,
                            _fixture.Create<ByteOffset>(),
                            length,
                            decimalCount
                        )
                    )
            );
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(19)]
        [InlineData(254)]
        public void CreateFailsIfFieldLengthIsOutOfRange(int outOfRange)
        {
            var length = new DbaseFieldLength(outOfRange);
            var decimalCount = new DbaseDecimalCount(0);
            Assert.Throws<ArgumentException>(
                () =>
                    new DbaseNumber(
                        new DbaseField(
                            _fixture.Create<DbaseFieldName>(),
                            DbaseFieldType.Number,
                            _fixture.Create<ByteOffset>(),
                            length,
                            decimalCount
                        )
                    )
            );
        }

        [Fact]
        public void IsDbaseFieldValue()
        {
            Assert.IsAssignableFrom<DbaseFieldValue>(_fixture.Create<DbaseNumber>());
        }

        [Fact]
        public void ReaderCanNotBeNull()
        {
            new GuardClauseAssertion(_fixture)
                .Verify(new Methods<DbaseNumber>().Select(instance => instance.Read(null)));
        }

        [Fact]
        public void WriterCanNotBeNull()
        {
            new GuardClauseAssertion(_fixture)
                .Verify(new Methods<DbaseNumber>().Select(instance => instance.Write(null)));
        }

        [Fact]
        public void LengthOfPositiveValueBeingSetCanNotExceedFieldLength()
        {
            var length = DbaseNumber.MaximumLength;
            var decimalCount = _fixture.GenerateDbaseDoubleDecimalCount(length);

            var sut =
                new DbaseNumber(
                    new DbaseField(
                        _fixture.Create<DbaseFieldName>(),
                        DbaseFieldType.Number,
                        _fixture.Create<ByteOffset>(),
                        length,
                        decimalCount
                    )
                );

            Assert.Throws<ArgumentException>(() => sut.Value = double.MaxValue);
        }

        [Fact]
        public void LengthOfNegativeValueBeingSetCanNotExceedFieldLength()
        {
            var length = DbaseNumber.MaximumLength;
            var decimalCount = _fixture.GenerateDbaseDoubleDecimalCount(length);

            var sut =
                new DbaseNumber(
                    new DbaseField(
                        _fixture.Create<DbaseFieldName>(),
                        DbaseFieldType.Number,
                        _fixture.Create<ByteOffset>(),
                        length,
                        decimalCount
                    )
                );

            Assert.Throws<ArgumentException>(() => sut.Value = double.MinValue);
        }

        [Fact]
        public void CanReadWriteNull()
        {
            var sut = _fixture.Create<DbaseNumber>();
            sut.Value = null;

            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream, Encoding.ASCII, true))
                {
                    sut.Write(writer);
                    writer.Flush();
                }

                stream.Position = 0;

                using (var reader = new BinaryReader(stream, Encoding.ASCII, true))
                {
                    var result = new DbaseNumber(sut.Field);
                    result.Read(reader);

                    Assert.Equal(sut.Field, result.Field);
                    Assert.Equal(sut.Value, result.Value);
                }
            }
        }

        [Fact]
        public void CanReadWriteNegative()
        {
            using (var random = new PooledRandom())
            {
                var sut = new DbaseNumber(
                    new DbaseField(
                        _fixture.Create<DbaseFieldName>(),
                        DbaseFieldType.Number,
                        _fixture.Create<ByteOffset>(),
                        DbaseNumber.NegativeValueMinimumLength,
                        new DbaseDecimalCount(1)
                    )
                );
                sut.Value =
                    new DbaseFieldNumberGenerator(random)
                        .GenerateAcceptableValue(sut);

                using (var stream = new MemoryStream())
                {
                    using (var writer = new BinaryWriter(stream, Encoding.ASCII, true))
                    {
                        sut.Write(writer);
                        writer.Flush();
                    }

                    stream.Position = 0;

                    using (var reader = new BinaryReader(stream, Encoding.ASCII, true))
                    {
                        var result = new DbaseNumber(sut.Field);
                        result.Read(reader);

                        Assert.Equal(sut, result, new DbaseFieldValueEqualityComparer());
                    }
                }
            }
        }


        [Fact]
        public void CanReadWriteWithMaxDecimalCount()
        {
            var length = DbaseNumber.MaximumLength;
            var decimalCount = DbaseNumber.MaximumDecimalCount;
            var sut =
                new DbaseNumber(
                    new DbaseField(
                        _fixture.Create<DbaseFieldName>(),
                        DbaseFieldType.Number,
                        _fixture.Create<ByteOffset>(),
                        length,
                        decimalCount
                    )
                );

            using (var random = new PooledRandom())
            {
                sut.Value =
                    new DbaseFieldNumberGenerator(random)
                        .GenerateAcceptableValue(sut);

                using (var stream = new MemoryStream())
                {
                    using (var writer = new BinaryWriter(stream, Encoding.ASCII, true))
                    {
                        sut.Write(writer);
                        writer.Flush();
                    }

                    stream.Position = 0;

                    using (var reader = new BinaryReader(stream, Encoding.ASCII, true))
                    {
                        var result = new DbaseNumber(sut.Field);
                        result.Read(reader);

                        Assert.Equal(sut.Field, result.Field);
                        Assert.Equal(sut.Value, result.Value);
                    }
                }
            }
        }

        [Fact]
        public void CanReadWrite()
        {
            var sut = _fixture.Create<DbaseNumber>();

            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream, Encoding.ASCII, true))
                {
                    sut.Write(writer);
                    writer.Flush();
                }

                stream.Position = 0;

                using (var reader = new BinaryReader(stream, Encoding.ASCII, true))
                {
                    var result = new DbaseNumber(sut.Field);
                    result.Read(reader);

                    Assert.Equal(sut.Field, result.Field);
                    Assert.Equal(sut.Value, result.Value);
                }
            }
        }

        [Fact]
        public void CanNotReadPastEndOfStream()
        {
            var sut = _fixture.Create<DbaseNumber>();

            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream, Encoding.ASCII, true))
                {
                    writer.Write(_fixture.CreateMany<byte>(new Random().Next(0, sut.Field.Length.ToInt32())).ToArray());
                    writer.Flush();
                }

                stream.Position = 0;

                using (var reader = new BinaryReader(stream, Encoding.ASCII, true))
                {
                    var result = new DbaseNumber(sut.Field);
                    Assert.Throws<EndOfStreamException>(() => result.Read(reader));
                }
            }
        }

        [Fact]
        public void WritesExcessDecimalsAsZero()
        {
            var length = _fixture.GenerateDbaseDoubleLength();
            var decimalCount = _fixture.GenerateDbaseDoubleDecimalCount(length);
            var sut = new DbaseNumber(
                new DbaseField(
                    _fixture.Create<DbaseFieldName>(),
                    DbaseFieldType.Number,
                    _fixture.Create<ByteOffset>(),
                    length,
                    decimalCount
                ), 0.0);

            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream, Encoding.ASCII, true))
                {
                    sut.Write(writer);
                    writer.Flush();
                }

                stream.Position = 0;

                if (decimalCount.ToInt32() == 0)
                {
                    Assert.Equal(
                        "0".PadLeft(length.ToInt32()),
                        Encoding.ASCII.GetString(stream.ToArray()));
                }
                else
                {
                    Assert.Equal(
                        new string(' ', length.ToInt32() - decimalCount.ToInt32() - 2)
                        + "0."
                        + new string('0', decimalCount.ToInt32()),
                        Encoding.ASCII.GetString(stream.ToArray())
                    );
                }
            }
        }

        // [Theory]
        // [InlineData(10, "01234567890", false)]
        // [InlineData(10, null, true)]
        // [InlineData(10, "", true)]
        // [InlineData(10, "0", true)]
        // [InlineData(10, "012345678", true)]
        // [InlineData(10, "0123456789", true)]
        // public void AcceptsStringValueReturnsExpectedResult(int length, string value, bool accepted)
        // {
        //     var sut = new DbaseCharacter(
        //         DbaseField.CreateCharacterField(
        //             _fixture.Create<DbaseFieldName>(),
        //             new DbaseFieldLength(length)
        //         ));
        //
        //     var result = sut.AcceptsValue(value);
        //
        //     Assert.Equal(accepted, result);
        // }

        [Theory]
        [MemberData(nameof(AcceptsNullableInt32ValueCases))]
        public void AcceptsNullableInt32ValueReturnsExpectedResult(int length, int? value, bool accepted)
        {
            var sut = new DbaseNumber(
                DbaseField.CreateNumberField(
                    _fixture.Create<DbaseFieldName>(),
                    new DbaseFieldLength(length),
                    new DbaseDecimalCount(0)
                ));

            var result = sut.AcceptsValue(value);

            Assert.Equal(accepted, result);
        }

        public static IEnumerable<object[]> AcceptsNullableInt32ValueCases
        {
            get
            {
                yield return new object[]
                {
                    3,
                    null,
                    true
                };

                yield return new object[]
                {
                    3,
                    1000,
                    false
                };

                yield return new object[]
                {
                    4,
                    1000,
                    true
                };

                yield return new object[]
                {
                    5,
                    1000,
                    true
                };

                yield return new object[]
                {
                    4,
                    -1000,
                    false
                };

                yield return new object[]
                {
                    5,
                    -1000,
                    true
                };
            }
        }

        [Theory]
        [MemberData(nameof(AcceptsNullableInt16ValueCases))]
        public void AcceptsNullableInt16ValueReturnsExpectedResult(int length, short? value, bool accepted)
        {
            var sut = new DbaseNumber(
                DbaseField.CreateNumberField(
                    _fixture.Create<DbaseFieldName>(),
                    new DbaseFieldLength(length),
                    new DbaseDecimalCount(0)
                ));

            var result = sut.AcceptsValue(value);

            Assert.Equal(accepted, result);
        }

        public static IEnumerable<object[]> AcceptsNullableInt16ValueCases
        {
            get
            {
                yield return new object[]
                {
                    3,
                    null,
                    true
                };

                yield return new object[]
                {
                    3,
                    new short?(1000),
                    false
                };

                yield return new object[]
                {
                    4,
                    new short?(1000),
                    true
                };

                yield return new object[]
                {
                    5,
                    new short?(1000),
                    true
                };

                yield return new object[]
                {
                    4,
                    new short?(-1000),
                    false
                };

                yield return new object[]
                {
                    5,
                    new short?(-1000),
                    true
                };
            }
        }

        [Theory]
        [MemberData(nameof(TryGetValueAsNullableInt32Cases))]
        public void TryGetValueAsNullableInt32ReturnsExpectedResult(int length, double? value, bool gotten, int? gottenValueAsInt32)
        {
            var sut = new DbaseNumber(
                DbaseField.CreateNumberField(
                    _fixture.Create<DbaseFieldName>(),
                    new DbaseFieldLength(length),
                    new DbaseDecimalCount(0)
                ), value);

            var result = sut.TryGetValueAsNullableInt32(out var valueAsInt32);

            Assert.Equal(gotten, result);
            Assert.Equal(gottenValueAsInt32, valueAsInt32);
        }

        public static IEnumerable<object[]> TryGetValueAsNullableInt32Cases
        {
            get
            {
                yield return new object[]
                {
                    5,
                    22.0,
                    true,
                    new int?(22)
                };

                yield return new object[]
                {
                    5,
                    null,
                    true,
                    new int?()
                };

                yield return new object[]
                {
                    5,
                    -22.0,
                    true,
                    new int?(-22)
                };

                yield return new object[]
                {
                    DbaseNumber.MaximumLength.ToInt32(),
                    Convert.ToDouble(int.MaxValue + 1.0),
                    false,
                    new int?()
                };

                yield return new object[]
                {
                    DbaseNumber.MaximumLength.ToInt32(),
                    Convert.ToDouble(int.MinValue - 1.0),
                    false,
                    new int?()
                };
            }
        }

        [Theory]
        [MemberData(nameof(TryGetValueAsNullableInt16Cases))]
        public void TryGetValueAsNullableInt16ReturnsExpectedResult(int length, double? value, bool gotten, short? gottenValueAsInt16)
        {
            var sut = new DbaseNumber(
                DbaseField.CreateNumberField(
                    _fixture.Create<DbaseFieldName>(),
                    new DbaseFieldLength(length),
                    new DbaseDecimalCount(0)
                ), value);

            var result = sut.TryGetValueAsNullableInt16(out var valueAsInt16);

            Assert.Equal(gotten, result);
            Assert.Equal(gottenValueAsInt16, valueAsInt16);
        }

        public static IEnumerable<object[]> TryGetValueAsNullableInt16Cases
        {
            get
            {
                yield return new object[]
                {
                    5,
                    22.0,
                    true,
                    new short?(22)
                };

                yield return new object[]
                {
                    5,
                    null,
                    true,
                    new short?()
                };

                yield return new object[]
                {
                    5,
                    -22.0,
                    true,
                    new short?(-22)
                };

                yield return new object[]
                {
                    DbaseNumber.MaximumLength.ToInt32(),
                    Convert.ToDouble(short.MaxValue + 1.0),
                    false,
                    new short?()
                };

                yield return new object[]
                {
                    DbaseNumber.MaximumLength.ToInt32(),
                    Convert.ToDouble(short.MinValue - 1.0),
                    false,
                    new short?()
                };
            }
        }

        [Theory]
        [MemberData(nameof(TrySetValueAsNullableInt32Cases))]
        public void TrySetValueAsNullableInt32ReturnsExpectedResult(int length, int? value, bool expected)
        {
            var sut = new DbaseNumber(
                DbaseField.CreateNumberField(
                    _fixture.Create<DbaseFieldName>(),
                    new DbaseFieldLength(length),
                    new DbaseDecimalCount(0)
                ));

            var result = sut.TrySetValueAsNullableInt32(value);

            Assert.Equal(expected, result);
        }

        public static IEnumerable<object[]> TrySetValueAsNullableInt32Cases
        {
            get
            {
                yield return new object[]
                {
                    5,
                    new int?(22),
                    true
                };

                yield return new object[]
                {
                    5,
                    new int?(),
                    true
                };

                yield return new object[]
                {
                    5,
                    new int?(-22),
                    true
                };

                yield return new object[]
                {
                    5,
                    new int?(int.MaxValue),
                    false
                };

                yield return new object[]
                {
                    5,
                    new int?(int.MinValue),
                    false
                };
            }
        }

        [Theory]
        [MemberData(nameof(TrySetValueAsNullableInt16Cases))]
        public void TrySetValueAsNullableInt16ReturnsExpectedResult(int length, short? value, bool expected)
        {
            var sut = new DbaseNumber(
                DbaseField.CreateNumberField(
                    _fixture.Create<DbaseFieldName>(),
                    new DbaseFieldLength(length),
                    new DbaseDecimalCount(0)
                ));

            var result = sut.TrySetValueAsNullableInt16(value);

            Assert.Equal(expected, result);
        }

        public static IEnumerable<object[]> TrySetValueAsNullableInt16Cases
        {
            get
            {
                yield return new object[]
                {
                    3,
                    new short?(22),
                    true
                };

                yield return new object[]
                {
                    3,
                    new short?(),
                    true
                };

                yield return new object[]
                {
                    3,
                    new short?(-22),
                    true
                };

                yield return new object[]
                {
                    3,
                    new short?(short.MaxValue),
                    false
                };

                yield return new object[]
                {
                    3,
                    new short?(short.MinValue),
                    false
                };
            }
        }

        [Theory]
        [MemberData(nameof(TryGetValueAsNullableInt32Cases))]
        public void GetValueAsNullableInt32ReturnsExpectedResult(int length, double? value,
            bool gotten, int? gottenValueAsInt32)
        {
            var sut = new DbaseNumber(
                DbaseField.CreateNumberField(
                    _fixture.Create<DbaseFieldName>(),
                    new DbaseFieldLength(length),
                    new DbaseDecimalCount(0)
                ), value);

            if (!gotten)
            {
                Assert.Throws<FormatException>(() =>
                {
                    var _ = sut.ValueAsNullableInt32;
                });
            }
            else
            {
                var result = sut.ValueAsNullableInt32;
                Assert.Equal(gottenValueAsInt32, result);
            }
        }

        [Theory]
        [MemberData(nameof(TrySetValueAsNullableInt32Cases))]
        public void SetValueAsNullableInt32ReturnsExpectedResult(int length, int? value, bool expected)
        {
            var sut = new DbaseNumber(
                DbaseField.CreateNumberField(
                    _fixture.Create<DbaseFieldName>(),
                    new DbaseFieldLength(length),
                    new DbaseDecimalCount(0)
                ));

            if (!expected)
            {
                Assert.Throws<FormatException>(() =>
                {
                    sut.ValueAsNullableInt32 = value;
                });
            }
            else
            {
                sut.ValueAsNullableInt32 = value;
            }
        }

        [Theory]
        [MemberData(nameof(TryGetValueAsNullableInt16Cases))]
        public void GetValueAsNullableInt16ReturnsExpectedResult(int length, double? value, bool gotten, short? gottenValueAsInt16)
        {
            var sut = new DbaseNumber(
                DbaseField.CreateNumberField(
                    _fixture.Create<DbaseFieldName>(),
                    new DbaseFieldLength(length),
                    new DbaseDecimalCount(0)
                ), value);

            if (!gotten)
            {
                Assert.Throws<FormatException>(() =>
                {
                    var _ = sut.ValueAsNullableInt16;
                });
            }
            else
            {
                var result = sut.ValueAsNullableInt16;
                Assert.Equal(gottenValueAsInt16, result);
            }
        }

        [Theory]
        [MemberData(nameof(TrySetValueAsNullableInt16Cases))]
        public void SetValueAsNullableInt16ReturnsExpectedResult(int length, short? value, bool expected)
        {
            var sut = new DbaseNumber(
                DbaseField.CreateNumberField(
                    _fixture.Create<DbaseFieldName>(),
                    new DbaseFieldLength(length),
                    new DbaseDecimalCount(0)
                ));

            if (!expected)
            {
                Assert.Throws<FormatException>(() =>
                {
                    sut.ValueAsNullableInt16 = value;
                });
            }
            else
            {
                sut.ValueAsNullableInt16 = value;
            }
        }

                [Theory]
        [MemberData(nameof(AcceptsInt32ValueCases))]
        public void AcceptsInt32ValueReturnsExpectedResult(int length, int value, bool accepted)
        {
            var sut = new DbaseNumber(
                DbaseField.CreateNumberField(
                    _fixture.Create<DbaseFieldName>(),
                    new DbaseFieldLength(length),
                    new DbaseDecimalCount(0)
                ));

            var result = sut.AcceptsValue(value);

            Assert.Equal(accepted, result);
        }

        public static IEnumerable<object[]> AcceptsInt32ValueCases
        {
            get
            {
                yield return new object[]
                {
                    3,
                    1000,
                    false
                };

                yield return new object[]
                {
                    4,
                    1000,
                    true
                };

                yield return new object[]
                {
                    5,
                    1000,
                    true
                };

                yield return new object[]
                {
                    4,
                    -1000,
                    false
                };

                yield return new object[]
                {
                    5,
                    -1000,
                    true
                };
            }
        }

        [Theory]
        [MemberData(nameof(AcceptsInt16ValueCases))]
        public void AcceptsInt16ValueReturnsExpectedResult(int length, short value, bool accepted)
        {
            var sut = new DbaseNumber(
                DbaseField.CreateNumberField(
                    _fixture.Create<DbaseFieldName>(),
                    new DbaseFieldLength(length),
                    new DbaseDecimalCount(0)
                ));

            var result = sut.AcceptsValue(value);

            Assert.Equal(accepted, result);
        }

        public static IEnumerable<object[]> AcceptsInt16ValueCases
        {
            get
            {
                yield return new object[]
                {
                    3,
                    (short)1000,
                    false
                };

                yield return new object[]
                {
                    4,
                    (short)1000,
                    true
                };

                yield return new object[]
                {
                    5,
                    (short)1000,
                    true
                };

                yield return new object[]
                {
                    4,
                    (short)-1000,
                    false
                };

                yield return new object[]
                {
                    5,
                    (short)-1000,
                    true
                };
            }
        }

        [Theory]
        [MemberData(nameof(TryGetValueAsInt32Cases))]
        public void TryGetValueAsInt32ReturnsExpectedResult(int length, double? value, bool gotten, int gottenValueAsInt32)
        {
            var sut = new DbaseNumber(
                DbaseField.CreateNumberField(
                    _fixture.Create<DbaseFieldName>(),
                    new DbaseFieldLength(length),
                    new DbaseDecimalCount(0)
                ), value);

            var result = sut.TryGetValueAsInt32(out var valueAsInt32);

            Assert.Equal(gotten, result);
            Assert.Equal(gottenValueAsInt32, valueAsInt32);
        }

        public static IEnumerable<object[]> TryGetValueAsInt32Cases
        {
            get
            {
                yield return new object[]
                {
                    5,
                    22.0,
                    true,
                    22
                };

                yield return new object[]
                {
                    5,
                    null,
                    false,
                    default(int)
                };

                yield return new object[]
                {
                    5,
                    -22.0,
                    true,
                    -22
                };

                yield return new object[]
                {
                    DbaseNumber.MaximumLength.ToInt32(),
                    Convert.ToDouble(int.MaxValue + 1.0),
                    false,
                    default(int)
                };

                yield return new object[]
                {
                    DbaseNumber.MaximumLength.ToInt32(),
                    Convert.ToDouble(int.MinValue - 1.0),
                    false,
                    default(int)
                };
            }
        }

        [Theory]
        [MemberData(nameof(TryGetValueAsInt16Cases))]
        public void TryGetValueAsInt16ReturnsExpectedResult(int length, double? value, bool gotten, short gottenValueAsInt16)
        {
            var sut = new DbaseNumber(
                DbaseField.CreateNumberField(
                    _fixture.Create<DbaseFieldName>(),
                    new DbaseFieldLength(length),
                    new DbaseDecimalCount(0)
                ), value);

            var result = sut.TryGetValueAsInt16(out var valueAsInt16);

            Assert.Equal(gotten, result);
            Assert.Equal(gottenValueAsInt16, valueAsInt16);
        }

        public static IEnumerable<object[]> TryGetValueAsInt16Cases
        {
            get
            {
                yield return new object[]
                {
                    5,
                    22.0,
                    true,
                    (short)22
                };

                yield return new object[]
                {
                    5,
                    null,
                    false,
                    default(short)
                };

                yield return new object[]
                {
                    5,
                    -22.0,
                    true,
                    (short)-22
                };

                yield return new object[]
                {
                    DbaseNumber.MaximumLength.ToInt32(),
                    Convert.ToDouble(short.MaxValue + 1.0),
                    false,
                    default(short)
                };

                yield return new object[]
                {
                    DbaseNumber.MaximumLength.ToInt32(),
                    Convert.ToDouble(short.MinValue - 1.0),
                    false,
                    default(short)
                };
            }
        }

        [Theory]
        [MemberData(nameof(TrySetValueAsInt32Cases))]
        public void TrySetValueAsInt32ReturnsExpectedResult(int length, int value, bool expected)
        {
            var sut = new DbaseNumber(
                DbaseField.CreateNumberField(
                    _fixture.Create<DbaseFieldName>(),
                    new DbaseFieldLength(length),
                    new DbaseDecimalCount(0)
                ));

            var result = sut.TrySetValueAsInt32(value);

            Assert.Equal(expected, result);
        }

        public static IEnumerable<object[]> TrySetValueAsInt32Cases
        {
            get
            {
                yield return new object[]
                {
                    5,
                    22,
                    true
                };

                yield return new object[]
                {
                    5,
                    -22,
                    true
                };

                yield return new object[]
                {
                    5,
                    int.MaxValue,
                    false
                };

                yield return new object[]
                {
                    5,
                    int.MinValue,
                    false
                };
            }
        }

        [Theory]
        [MemberData(nameof(TrySetValueAsInt16Cases))]
        public void TrySetValueAsInt16ReturnsExpectedResult(int length, short value, bool expected)
        {
            var sut = new DbaseNumber(
                DbaseField.CreateNumberField(
                    _fixture.Create<DbaseFieldName>(),
                    new DbaseFieldLength(length),
                    new DbaseDecimalCount(0)
                ));

            var result = sut.TrySetValueAsInt16(value);

            Assert.Equal(expected, result);
        }

        public static IEnumerable<object[]> TrySetValueAsInt16Cases
        {
            get
            {
                yield return new object[]
                {
                    3,
                    (short)22,
                    true
                };

                yield return new object[]
                {
                    3,
                    (short)-22,
                    true
                };

                yield return new object[]
                {
                    3,
                    short.MaxValue,
                    false
                };

                yield return new object[]
                {
                    3,
                    short.MinValue,
                    false
                };
            }
        }

        [Theory]
        [MemberData(nameof(TryGetValueAsInt32Cases))]
        public void GetValueAsInt32ReturnsExpectedResult(int length, double? value,
            bool gotten, int gottenValueAsInt32)
        {
            var sut = new DbaseNumber(
                DbaseField.CreateNumberField(
                    _fixture.Create<DbaseFieldName>(),
                    new DbaseFieldLength(length),
                    new DbaseDecimalCount(0)
                ), value);

            if (!gotten)
            {
                Assert.Throws<FormatException>(() =>
                {
                    var _ = sut.ValueAsInt32;
                });
            }
            else
            {
                var result = sut.ValueAsInt32;
                Assert.Equal(gottenValueAsInt32, result);
            }
        }

        [Theory]
        [MemberData(nameof(TrySetValueAsInt32Cases))]
        public void SetValueAsInt32ReturnsExpectedResult(int length, int value, bool expected)
        {
            var sut = new DbaseNumber(
                DbaseField.CreateNumberField(
                    _fixture.Create<DbaseFieldName>(),
                    new DbaseFieldLength(length),
                    new DbaseDecimalCount(0)
                ));

            if (!expected)
            {
                Assert.Throws<FormatException>(() =>
                {
                    sut.ValueAsInt32 = value;
                });
            }
            else
            {
                sut.ValueAsInt32 = value;
            }
        }

        [Theory]
        [MemberData(nameof(TryGetValueAsInt16Cases))]
        public void GetValueAsInt16ReturnsExpectedResult(int length, double? value, bool gotten, short gottenValueAsInt16)
        {
            var sut = new DbaseNumber(
                DbaseField.CreateNumberField(
                    _fixture.Create<DbaseFieldName>(),
                    new DbaseFieldLength(length),
                    new DbaseDecimalCount(0)
                ), value);

            if (!gotten)
            {
                Assert.Throws<FormatException>(() =>
                {
                    var _ = sut.ValueAsInt16;
                });
            }
            else
            {
                var result = sut.ValueAsInt16;
                Assert.Equal(gottenValueAsInt16, result);
            }
        }

        [Theory]
        [MemberData(nameof(TrySetValueAsInt16Cases))]
        public void SetValueAsInt16ReturnsExpectedResult(int length, short value, bool expected)
        {
            var sut = new DbaseNumber(
                DbaseField.CreateNumberField(
                    _fixture.Create<DbaseFieldName>(),
                    new DbaseFieldLength(length),
                    new DbaseDecimalCount(0)
                ));

            if (!expected)
            {
                Assert.Throws<FormatException>(() =>
                {
                    sut.ValueAsInt16 = value;
                });
            }
            else
            {
                sut.ValueAsInt16 = value;
            }
        }
    }
}
