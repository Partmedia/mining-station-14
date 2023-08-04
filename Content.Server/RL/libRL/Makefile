all: libRL.so

CFLAGS=-I/usr/local/include -I/usr/include
LDFLAGS=-L/usr/local/lib -L/usr/lib64

RL_wrap.c: RL.i
	swig $(CFLAGS) -csharp RL.i

libRL.so: RL_wrap.c
	$(CC) -fPIC $(CFLAGS) -c RL_wrap.c
	$(CC) -shared -o libRL.so RL_wrap.o $(LDFLAGS) -lecl
