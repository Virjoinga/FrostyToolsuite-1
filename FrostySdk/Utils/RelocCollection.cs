using FrostySdk.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace FrostySdk;

public readonly struct RelocOffset
{
    public readonly RelocChunk Chunk;
    public readonly long Offset;

    public RelocOffset(RelocChunk chunk, long offset)
    {
        Chunk = chunk;
        Offset = offset;
    }
}

public readonly struct Relocation
{
    public readonly string Name;
    public readonly RelocOffset Source;
    public readonly RelocOffset Destination;

    public Relocation(string name, RelocOffset src, RelocOffset dest)
    {
        Name = name;
        Source = src;
        Destination = dest;
    }

    public override string ToString()
    {
        return $"{Name} {Source.Chunk.Name}:0x{Source.Offset:x} -> {Destination.Chunk.Name}:0x{Destination.Offset:x}";
    }
}

public class RelocChunk
{
    public string Name = string.Empty;
    public long GlobalOffset = 0;
    public DataStream Writer { get; private set; }
    public long Size => m_stream.Length;
    public RelocCollection Collection { get; private set; }

    private MemoryStream m_stream = new();
    public SortedList<long, RelocOffset> m_relocs = new();

    public RelocChunk(string name, RelocCollection collection)
    {
        Name = name;
        Collection = collection;
        Writer = new(m_stream);
    }

    public RelocOffset GetOffset(long offset = 0, SeekOrigin origin = SeekOrigin.Current)
    {
        long desiredOffset = 0;
        switch (origin)
        {
            case SeekOrigin.Begin: desiredOffset = offset; break;
            case SeekOrigin.Current: desiredOffset = m_stream.Position + offset; break;
            case SeekOrigin.End: offset = (m_stream.Length - 1) + offset; break;
        }

        return new(this, desiredOffset);
    }

    public void AddReloc(string relocName, RelocOffset dest)
    {
        var src = GetOffset();
        Writer.WriteUInt64(0xDEADBEEF);

        Collection.AddReloc(relocName, src, dest);
    }

    public void Fixup()
    {
        foreach (var ptr in m_relocs)
        {
            Writer.Position = ptr.Key;
            Writer.WriteInt64(ptr.Value.Offset + ptr.Value.Chunk.GlobalOffset);
        }
    }

    public byte[] ToArray()
    {
        return m_stream.ToArray();
    }
}

public class RelocCollection
{
    public long TotalChunksSize { get; private set; } = 0;
    public long RelocTableSize { get; private set; } = 0;

    private Dictionary<string, RelocChunk> m_chunkNameMap = new();
    private List<RelocChunk> m_chunks = new();
    private readonly List<Relocation> m_relocs = new();

    public RelocChunk GetChunk(string name)
    {
        if (m_chunkNameMap.TryGetValue(name, out var seg))
            return seg;

        RelocChunk newChunk = new(name, this);
        m_chunks.Add(newChunk);
        m_chunkNameMap.Add(name, newChunk);
        return newChunk;
    }

    public void AddReloc(string relocName, RelocOffset src, RelocOffset dest)
    {
        m_relocs.Add(new Relocation(relocName, src, dest));
    }

    public void WriteChunks(DataStream writer)
    {
        long totalOffset = 0;

        foreach (var chunk in m_chunks)
        {
            chunk.GlobalOffset = totalOffset;
            chunk.Writer.PadWrite(16);

            writer.Write(chunk.ToArray());

            totalOffset += chunk.Writer.Length;
        }

        // fixup relocations
        foreach (var reloc in m_relocs)
        {
            writer.Position = reloc.Source.Offset + reloc.Source.Chunk.GlobalOffset;

            writer.WriteInt64(reloc.Destination.Offset + reloc.Destination.Chunk.GlobalOffset);
        }

        TotalChunksSize = totalOffset;
    }

    public void WriteRelocTable(DataStream writer)
    {
        long begin = writer.Position;
        foreach (var reloc in m_relocs)
        {
            long offset = reloc.Source.Offset + reloc.Source.Chunk.GlobalOffset;
            writer.WriteInt32((int)offset);
        }
        RelocTableSize = writer.Position - begin;
    }
}