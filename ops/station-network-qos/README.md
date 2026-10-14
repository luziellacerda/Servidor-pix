# Divisão automática da banda do Station

Aplicada em 13/10/2026 e atualizada em 14/10/2026. O controle cobre a saída **deste PC**, incluindo tráfego
local e para a internet. A API Station e o master Nginx conservaram seus processos.
Licenças, banco de produção, catálogo, motores, opções de emulação e APK não foram
alterados pela configuração de rede.

## Capacidade e empréstimo

O operador determinou **100 Mbps de upload e 800 Mbps de download**. Esses dados
definem a configuração atual; não houve teste de velocidade da operadora nesta
atualização. A interface suporta gigabit, mas o enlace físico continua negociado
a **100 Mbps/full duplex**. Portanto, 800 Mbps de recepção ainda não são possíveis
por esta ligação. Não forçar renegociação enquanto este PC atende outros serviços.

O teto atual de envio para a internet é **100 Mbps**; a saída da placa também tem
teto de 100 Mbps. Este controle é de saída: **não impõe teto à entrada de dados
neste PC**. Os 800 Mbps constam como capacidade informada do plano, sem afirmar
que foram alcançados ou criar uma limitação artificial menor no download.

Internet e rede local continuam em ramos próprios. Com o teto internet igual ao
da placa, as garantias dos ramos são 90 Mbps para internet e 10 Mbps para LAN;
ambos podem emprestar capacidade ociosa até 100 Mbps. A reserva LAN evita taxa
zero e não fixa a internet em 90 Mbps. O HTB distingue a garantia `rate` e o teto
`ceil` disponível por empréstimo. [Manual do HTB](https://man7.org/linux/man-pages/man8/tc-htb.8.html).
As reservas dentro de cada ramo são:

| Classe | Reserva mínima | Prioridade | Uso da capacidade ociosa |
| --- | --- | --- | --- |
| Partidas diretas, túneis existentes e ACKs pequenos | 20% | Alta | Permitido |
| Demais serviços | 20% | Média | Permitido |
| Downloads HTTPS | 60% | Baixa | Permitido |

No ramo internet, isso corresponde a 18 / 18 / 54 Mbps. Um download sozinho
pode aproveitar o teto inteiro de 100 Mbps; a reserva passa a ser utilizada pelas
outras classes conforme há tráfego. O controle considera a **soma dos fluxos**.
As classes têm FQ-CoDel para reduzir filas. Os 100 Mbps são teto configurado,
não promessa de velocidade por aparelho, resolução de instabilidade da Claro ou
homologação de gameplay. Limitar pela capacidade informada, acima da capacidade
que a operadora eventualmente entrega, pode deixar filas no equipamento externo.

Histórico da implantação de 13/10: teto internet 33 Mbps e placa 95 Mbps, apoiados
nos testes então executados. Estes valores foram substituídos em 14/10 por ordem
do operador. As medições anteriores não definem os tetos atuais.

Os túneis existentes transportam vários serviços criptografados na mesma conexão.
Eles compartilham a classe interativa. A separação específica de uma partida
direta usa o par IP/porta da conexão TLS, conferido contra o socket do Nginx. O
serviço de prioridade não recebe códigos, tickets, provas, cookies ou chaves.
Uma tentativa recusada encerra o HTTP keepalive, impedindo reutilização da conexão
marcada para baixar um artefato. A autorização continua na API, também quando o
serviço auxiliar de prioridade falha.

## Rota direta das partidas

```text
wss://downloads-station.lzgames.com.br/v1/station/online/multiplayer/relay
Sec-WebSocket-Protocol: station-stream.v3
Authorization: StationRelay <ticket novo>
X-Station-Request-Proof: <prova vinculada ao ticket e ao caminho canônico>
SPKI SHA256: bcd134d59fcdbb35e0343c86d31a666cc95cf158d70b27aceceefd19158d072f
```

A inclusão dessa rota é deliberada, preservando o GET de artefato. O certificado,
DNS e encaminhamento 443 existentes foram reutilizados. Demais APIs e diagnósticos
continuam bloqueados nesse host. `X-Station-Qos: 1` indica o cadastro da fila,
sem substituir qualquer verificação do ticket. O protocolo, replay, READY/ACK,
geração, janelas e limites da API permanecem iguais.

**A R141 e a estável R143 ainda usam a origem anterior para multiplayer.** A produção do APK deve
selecionar este URI e o pin correspondente somente na conexão multiplayer v3,
preservando origem/pin de login e comandos e a rota anterior do relay legado.
Modificar somente o URI, mantendo a fábrica TLS antiga, causaria erro de pin.
Usar a estável R143 como base, mantendo os parsers/seleção multitap atuais.
O detalhe de integração está no handoff existente. Os callbacks e os ajustes de
opções da R140 devem ser conservados; qualquer alteração de fingerprint deve
seguir a renovação normal dos owners e o kit privado.

## Operação e reversão

Arquivos: `/opt/station-network-qos`, `/etc/station-network-qos/config.json`,
duas inclusões específicas do Nginx e quatro unidades `station-network-*`.
O serviço principal é persistente no boot. Um timer confere a propriedade das
filas a cada 30 segundos e recupera a política apenas sobre a fila padrão conhecida.
Uma fila de outro administrador é recusada. A tabela nftables própria apenas
classifica tráfego; não altera regras de firewall, NAT ou roteamento existentes.

Consultar: `python3 /opt/station-network-qos/policy.py status`. Ajustar as taxas
no JSON protegido e recarregar `station-network-qos.service` preserva sockets e
filas existentes. Pausar somente o timer de conferência durante a troca atômica
dos arquivos, aguardar um check em andamento e restaurar/conferir o timer ao final.
Não reiniciar o serviço de QoS para trocar taxas: o restart retiraria suas filas.
As taxas atuais vêm da ordem do operador; não recalibrar por medição da Claro sem
nova solicitação. Após corrigir cabo/porta e obter enlace gigabit, revisar o teto
da placa para liberar a capacidade física nova, preservando o teto WAN de 100 Mbps.

Parar as unidades de prioridade/timer e depois `station-network-qos.service`
retira apenas esta classificação e devolve a fila padrão do kernel. O instalador
também restaura automaticamente o vhost se qualquer gate falhar. Não reaplicar
`deploy.py` quando o recibo privado `applied.json` já existir.

## Evidência

O recibo público fica no mesmo handoff, em
`docs/station-android/network-qos/server-return.json`. Inclui oito testes de
segurança da configuração, TLS/WebSocket real em fixture, quatro cenários de
filas Linux isoladas e duas conexões WSS com tickets/provas reais e licenças
sintéticas removidas. São provas de rede e admissão; gameplay Android ainda
depende do teste físico com a atualização do app.

O ajuste atual está em `docs/station-android/network-qos/limits-20261014.json`:
nove testes de segurança/configuração e seis cenários de tráfego em namespaces
Linux isolados, incluindo troca de teto durante conexões estabelecidas. No host,
reload de taxas preservou layout das filas, regras, PID/estado dos serviços,
catálogo, registros online e DLL. O check persistente passou. Testes isolados
não são medições da operadora nem prova de estabilidade da partida na internet.

Referências: [HTB](https://man7.org/linux/man-pages/man8/tc-htb.8.html),
[FQ-CoDel](https://www.rfc-editor.org/rfc/rfc8290.html),
[proxy WebSocket Nginx](https://nginx.org/en/docs/http/websocket.html).
