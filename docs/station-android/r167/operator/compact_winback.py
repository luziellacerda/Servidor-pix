"""Condense repeated instructions on six new WinBack bindings only.

All mode decisions, counts, documentary sources and historical rows stay exact.
The existing 128 KiB per-content validator is always used without modification.
"""
from copy import deepcopy

ITEM = 'station_4e577685b9feb98a02a1b2cb884f2925'
SOURCE = 'https://nintendo64.pl/wp-content/uploads/WinBack-Covert-Operations-USA.pdf'
COUNTS = {'death-match': [2, 3, 4], 'point-match': [3, 4], 'lethal-tag': [2],
          'cube-hunt': [2], 'quick-draw': [2], 'team-battle': [2]}

def compact(document):
    result = deepcopy(document)
    for mode in result['modes']:
        if mode['itemId'] != ITEM or not mode['profileId'].startswith('review-2222-'):
            continue
        if mode['mode'] not in COUNTS or mode['allowedPlayerCounts'] != COUNTS[mode['mode']] or mode['sources'] != [SOURCE]:
            raise ValueError('Exact original WinBack edition/mode/source required')
        title = mode['modeTitle'].removeprefix('WinBack: Covert Operations: ')
        counts = ', '.join(map(str, mode['allowedPlayerCounts']))
        mode['instructions'] = [
            f'Multiplayer / {title}: {counts} pessoas, cada uma com seu controle N64.',
            'Story é individual. A sala não aumenta a capacidade do modo.'
        ]
    return result
