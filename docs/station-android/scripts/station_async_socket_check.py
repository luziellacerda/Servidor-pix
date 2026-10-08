"""Blocking test adapter over a single asyncio TLS transport; never used by Station.

The historical sync Python TLS client timed out intermittently on immediate
Kestrel responses. Keep that control's evidence, and verify the release with
independent event-loop TLS I/O. No request proof, certificate check or deadline
is bypassed by this adapter.
"""
import asyncio
import threading
from websockets.asyncio.client import connect as async_connect


class SocketChecks:
    def __init__(self):
        self.loop = asyncio.new_event_loop()
        self.thread = threading.Thread(target=self.loop.run_forever, daemon=True)
        self.thread.start()
        self.sockets = []

    def run(self, coroutine, timeout=25):
        future = asyncio.run_coroutine_threadsafe(coroutine, self.loop)
        try:
            return future.result(timeout=timeout)
        except BaseException:
            future.cancel()
            raise

    def connect(self, uri, **kwargs):
        async def open_socket():
            return await async_connect(uri, **kwargs)
        raw = self.run(open_socket())
        owner = self

        class Adapter:
            subprotocol = raw.subprotocol

            @property
            def socket(self):
                class Certificate:
                    def getpeercert(self, binary_form=False):
                        return raw.transport.get_extra_info('ssl_object').getpeercert(binary_form=binary_form)
                return Certificate()

            @property
            def close_code(self):
                return raw.close_code

            def send(self, payload):
                owner.run(raw.send(payload))

            def recv(self, timeout=15):
                async def bounded():
                    return await asyncio.wait_for(raw.recv(), timeout)
                return owner.run(bounded(), timeout+2)

            def close(self):
                owner.run(raw.close(), 5)

        adapter = Adapter()
        self.sockets.append(adapter)
        return adapter

    def close(self):
        try:
            for socket in self.sockets:
                try:
                    socket.close()
                except Exception:
                    pass
        finally:
            self.loop.call_soon_threadsafe(self.loop.stop)
            self.thread.join(timeout=5)
            if self.thread.is_alive():
                raise RuntimeError('Qualification event loop did not stop')
            self.loop.close()
