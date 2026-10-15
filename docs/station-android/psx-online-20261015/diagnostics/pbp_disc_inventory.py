#!/usr/bin/env python3
"""Read the disc table and ISO boot descriptors of an owned PBP, without writes.

Container layout reference: the pinned upstream PCSX-ReARMed cdriso.c handlepbp.
This does not extract games or certify that a disc switch is supported online.
"""
import argparse
import json
from pathlib import Path
import re
import struct
import zlib


def require(ok, message):
    if not ok:
        raise ValueError(message)


def inventory(path):
    path = Path(path)
    before = path.stat()
    with path.open('rb') as stream:
        def read(offset, size):
            require(0 <= offset <= before.st_size and 0 <= size <= before.st_size-offset,
                    'Container read exceeds source bounds')
            stream.seek(offset)
            data = stream.read(size)
            require(len(data) == size, 'Short container read')
            return data

        header = read(0, 40)
        require(header[:4] == b'\x00PBP', 'PBP signature required')
        psar = struct.unpack_from('<I', header, 36)[0]
        signature = read(psar, 10)
        if signature.startswith(b'PSTITLEIMG'):
            offsets = []
            for value in struct.unpack('<8I', read(psar+0x200, 32)):
                if not value:
                    break
                offsets.append(psar+value)
        else:
            offsets = [psar]
        require(1 <= len(offsets) <= 8 and len(set(offsets)) == len(offsets), 'Invalid disc table')
        discs = []
        for number, base in enumerate(offsets):
            require(read(base, 10) == b'PSISOIMG00', 'Disc image signature required')
            cache = {}

            def sector(number):
                require(0 <= number < 600000, 'Sector outside diagnostic bound')
                block, within = divmod(number, 16)
                if block not in cache:
                    offset, size = struct.unpack_from('<II', read(base+0x4000+block*32, 32))
                    require(0 < size <= 2352*16+100, 'Compressed block outside bound')
                    compressed = read(base+0x100000+offset, size)
                    decoder = zlib.decompressobj(-15)
                    expanded = decoder.decompress(compressed, 2352*16+1)
                    require(decoder.eof and len(expanded) == 2352*16, 'Invalid bounded disc block')
                    cache[block] = expanded
                raw = cache[block][within*2352:(within+1)*2352]
                # PS1 data sectors are Mode 2 (24-byte header) or Mode 1 (16).
                require(raw[:12] == b'\x00'+b'\xff'*10+b'\x00' and raw[15] in (1, 2), 'Raw data sector required')
                start = 24 if raw[15] == 2 else 16
                return raw[start:start+2048]

            def extent(lba, size):
                require(0 < size <= 256*1024, 'ISO descriptor extent outside bound')
                return b''.join(sector(lba+i) for i in range((size+2047)//2048))[:size]

            pvd = sector(16)
            require(pvd[:7] == b'\x01CD001\x01', 'ISO9660 primary volume descriptor required')
            root_lba, root_bytes = struct.unpack_from('<I', pvd, 158)[0], struct.unpack_from('<I', pvd, 166)[0]
            directory = extent(root_lba, root_bytes)
            cursor = 0
            boot = None
            while cursor < len(directory):
                length = directory[cursor]
                if length == 0:
                    cursor = (cursor//2048+1)*2048
                    continue
                require(length >= 34 and cursor+length <= len(directory), 'Malformed ISO directory entry')
                entry = directory[cursor:cursor+length]
                name = entry[33:33+entry[32]].decode('ascii', errors='replace')
                if name.upper() == 'SYSTEM.CNF;1':
                    boot = extent(struct.unpack_from('<I', entry, 2)[0], struct.unpack_from('<I', entry, 10)[0])
                    break
                cursor += length
            require(boot is not None, 'SYSTEM.CNF missing from diagnostic ISO root')
            match = re.search(rb'(?im)^\s*BOOT\s*=\s*cdrom\s*:\s*[\\/]*([^\s;]+)', boot)
            require(match is not None, 'Disc boot executable missing')
            executable = match[1].decode('ascii', errors='strict')
            serial = re.sub(r'[^A-Za-z0-9]', '', executable.split('\\')[-1].split('/')[-1]).upper()
            discs.append(dict(index=number, volumeLabel=pvd[40:72].decode('ascii', errors='replace').strip(' \x00'),
                              bootExecutable=executable, normalizedBootSerial=serial))
    after = path.stat()
    require((before.st_dev, before.st_ino, before.st_size, before.st_mtime_ns) ==
            (after.st_dev, after.st_ino, after.st_size, after.st_mtime_ns), 'Source changed during inspection')
    return dict(discCount=len(discs), discs=discs, sourceUnchanged=True, method='bounded-readonly-pbp-table-and-iso9660-system-cnf')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('owned_pbp', type=Path)
    args = parser.parse_args()
    print(json.dumps(inventory(args.owned_pbp), ensure_ascii=False, indent=2))
