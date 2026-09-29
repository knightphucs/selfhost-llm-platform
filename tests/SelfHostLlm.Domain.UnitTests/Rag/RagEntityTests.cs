using SelfHostLlm.Domain.Rag;

namespace SelfHostLlm.Domain.UnitTests.Rag;

public sealed class RagEntityTests
{
    [Fact]
    public void CollectionCreate_WithBgeM3Dimension_Succeeds()
    {
        Collection.Create(Guid.NewGuid(), "Quy trình nội bộ", Guid.NewGuid(), EmbeddingConstants.Dimension, 512, 64, TestClock.Now)
            .IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData(768)]
    [InlineData(1536)]
    public void CollectionCreate_WithOtherDimension_ReturnsValidationError(int dim)
    {
        Collection.Create(Guid.NewGuid(), "docs", Guid.NewGuid(), dim, 512, 64, TestClock.Now)
            .Error!.Code.Should().Be("collection.embedding_dim.unsupported");
    }

    [Theory]
    [InlineData(512, 512)]
    [InlineData(256, 300)]
    public void CollectionCreate_WithOverlapNotSmallerThanSize_ReturnsValidationError(int size, int overlap)
    {
        Collection.Create(Guid.NewGuid(), "docs", Guid.NewGuid(), EmbeddingConstants.Dimension, size, overlap, TestClock.Now)
            .Error!.Code.Should().Be("collection.chunk_overlap.too_large");
    }

    [Fact]
    public void ChunkCreate_WithCorrectDimension_CopiesEmbedding()
    {
        var vector = new float[EmbeddingConstants.Dimension];
        vector[0] = 0.5f;

        var chunk = Chunk.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 0, "nội dung", vector, 12).Value;
        vector[0] = 99f;

        chunk.Embedding.Length.Should().Be(EmbeddingConstants.Dimension);
        chunk.Embedding.Span[0].Should().Be(0.5f);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(768)]
    [InlineData(1025)]
    public void ChunkCreate_WithWrongDimension_ReturnsValidationError(int dim)
    {
        Chunk.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 0, "nội dung", new float[dim], 12)
            .Error!.Code.Should().Be("chunk.embedding.dimension_mismatch");
    }

    [Fact]
    public void DocumentCreate_WithInvalidContentHash_ReturnsValidationError()
    {
        Document.Create(Guid.NewGuid(), Guid.NewGuid(), "Sổ tay", null, "not-a-hash", "text/markdown", TestClock.Now)
            .Error!.Code.Should().Be("document.content_hash.invalid");
    }
}
