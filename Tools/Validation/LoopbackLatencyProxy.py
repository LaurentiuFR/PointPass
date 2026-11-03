"""Opt-in localhost UDP impairment for two-player tests. Does not alter game authority."""
import argparse
import asyncio
import json
from pathlib import Path
import random
import sys

async def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--listen',type=int,required=True)
    parser.add_argument('--server',type=int,required=True)
    parser.add_argument('--delay',type=float,default=70)
    parser.add_argument('--jitter',type=float,default=15)
    parser.add_argument('--loss',type=float,default=.01)
    parser.add_argument('--report',required=True)
    args=parser.parse_args()
    loop=asyncio.get_running_loop()
    rng=random.Random(401)
    stats={'delayMsPerLeg':args.delay,'jitterMsPerLeg':args.jitter,'configuredLoss':args.loss,'received':0,'dropped':0,'forwarded':0}
    stats['endpoints']=[]
    def schedule(data,send):
        stats['received']+=1
        if rng.random()<args.loss:stats['dropped']+=1;return
        delay=max(0,args.delay+rng.uniform(-args.jitter,args.jitter))/1000
        def forward():
            send(data);stats['forwarded']+=1
        loop.call_later(delay,forward)

    class Upstream(asyncio.DatagramProtocol):
        def __init__(self,client,downstream):self.client=client;self.downstream=downstream
        def connection_made(self,transport):self.transport=transport
        def datagram_received(self,data,address):
            schedule(data,lambda packet:self.downstream.sendto(packet,self.client))

    class Proxy(asyncio.DatagramProtocol):
        def __init__(self):self.clients={};self.pending={}
        def connection_made(self,transport):self.transport=transport
        async def create_upstream(self,address):
            # Each real client source gets its own server-facing UDP endpoint, as
            # it would through NAT. Reusing one endpoint for reconnects can make
            # the transport treat a fresh connection as the closed connection.
            transport,_=await loop.create_datagram_endpoint(
                lambda:Upstream(address,self.transport),
                remote_addr=('127.0.0.1',args.server))
            self.clients[address]=transport
            stats['endpoints'].append({'client':address,'upstream':transport.get_extra_info('sockname')})
            for packet in self.pending.pop(address):
                schedule(packet,transport.sendto)
        def datagram_received(self,data,address):
            if address in self.clients:
                schedule(data,self.clients[address].sendto)
            elif address in self.pending:self.pending[address].append(data)
            else:
                self.pending[address]=[data]
                loop.create_task(self.create_upstream(address))
    transport,proxy=await loop.create_datagram_endpoint(Proxy,local_addr=('127.0.0.1',args.listen))
    try:
        while True:
            Path(args.report).write_text(json.dumps(stats),encoding='utf-8')
            await asyncio.sleep(.5)
    finally:
        transport.close()
        for upstream in proxy.clients.values():upstream.close()

if __name__=='__main__':
    # Python's Windows proactor UDP reader can stop after an ICMP error from a
    # closed client. The selector loop keeps the relay readable for reconnects.
    if sys.platform=='win32':asyncio.set_event_loop_policy(asyncio.WindowsSelectorEventLoopPolicy())
    asyncio.run(main())
