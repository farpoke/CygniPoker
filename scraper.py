#!/usr/bin/python3
# -*- encoding: utf-8 -*-

from collections import namedtuple, OrderedDict
import json
from math import sqrt
from pprint import pprint
from time import sleep
import urllib.request

PlayerInfo = namedtuple('PlayerInfo',
                        'name first_seen last_seen wins losses faults history')

URL_FORMAT = 'http://poker.cygni.se/timemachine/table/%i/gameround/-1'

QUICK_SLEEP_TIME = 0.1
SLOW_SLEEP_TIME = 300

IGNORE_PLAYERS = ['Cautious',
                  'Raiser',
                  'Weighted',
                  'Sensible',
                  'Hellmuth']

RECENT_MIN_LENGTH = 5
RECENT_MAX_LENGTH = 50

SHOW_ALL_LIMIT = 10

seen_players = OrderedDict()
show_all = True

table_id = 900

def saw_player(data):
    name = data['name']
    chips = data['chipsAfterGame']
    #
    if name in seen_players:
        info = seen_players[name]
        first_seen = info.first_seen
        wins = info.wins
        losses = info.losses
        faults = info.faults
        history = info.history
    else:
        first_seen = table_id
        wins = 0
        losses = 0
        faults = 0
        history = []
    #
    if chips == 60000:
        wins += 1
        history.append(100)
    elif chips == 0:
        losses += 1
        history.append(0)
    else:
        faults += 1
    #
    seen_players[name] = PlayerInfo(name, first_seen, table_id,
                                    wins, losses, faults, history)

def fetch_table():
    url = URL_FORMAT % table_id
    response = urllib.request.urlopen(url)
    string_data = response.readall().decode('utf-8')
    obj = json.loads(string_data)
    #
    if obj['tableCounter'] > table_id:
        return 1  # Got wrong table back, try next id.
    elif obj['tableCounter'] < table_id:
        return 0  # Table not there yet, try later.
    elif not obj['lastGame']:
        return 0  # Game not finished, try later.
    #
    for player in obj['players']:
        if player['name'] not in IGNORE_PLAYERS:
            saw_player(player)
    #
    return 2;

def compute_recent(info):
    if len(info.history) < RECENT_MIN_LENGTH:
        return None, None
    else:
        recent_data = info.history[-RECENT_MAX_LENGTH:]
        recent = sum(recent_data) / len(recent_data)
        error = 1.96 * sqrt(recent * (100 - recent) / len(recent_data))
        return recent, error

def sort_key(info):
    recent, error = compute_recent(info)
    if recent is None:
        return info.wins / (info.wins + info.losses + info.faults)
    else:
        return recent - error

def output_statistics():
    global show_all
    #
    print('#%i' % (table_id-1), flush=True)
    #
    has_recent_count = 0
    for info in sorted(seen_players.values(), key=sort_key, reverse=True):
        recent, error = compute_recent(info)
        #
        if recent is None and not show_all:
            continue
        elif recent is not None:
            has_recent_count += 1
        #       
        print('%-60s %4i %4i %3i %3i %3i %11s %s' % (
            info.name.ljust(60, '.'),
            info.first_seen, info.last_seen,
            info.wins, info.losses, info.faults,
            ('%3.0f%% ±%4.1f%%' % (recent, error)) if recent is not None else ' - - - - - ',
            '<--' if info.last_seen == (table_id-1) else ''
            ))
    #
    print(flush=True)
    if has_recent_count >= SHOW_ALL_LIMIT:
        show_all = False

print('Starting scrape...', flush=True)
while True:
    result = fetch_table()
    if result > 0:
        table_id += 1
        if result == 2:
            output_statistics()
        sleep(QUICK_SLEEP_TIME)
    else:
        for second in range(SLOW_SLEEP_TIME):
            sleep(1)
            print('%5i/%-5i' % (second, SLOW_SLEEP_TIME), end='\r', flush=True)
        print(flush=True)
