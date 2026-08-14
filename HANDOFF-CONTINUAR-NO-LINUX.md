# Handoff — continuar o TurboRama no servidor Linux

Atualizado em: 2026-08-07

## Instrução para a nova conversa

O usuário está agora no servidor Linux. Leia este arquivo completamente antes de orientar qualquer
comando. Continue do estado descrito aqui; não reinicie a análise e não use comandos Windows.

O servidor Linux já mantém um site e um Cloudflare Tunnel funcionando 24 horas. A prioridade é não
interromper, substituir ou apagar nenhuma configuração existente.

## Estado concluído no Windows

- backend `TurboRamaPixOnlineServer` criado em ASP.NET Core/.NET 8;
- compilação concluída com zero erros e zero avisos;
- autoteste aprovado sem cobrança real;
- servidor exige licença, máquina, prova RSA-PSS e sessão autorizada;
- tentativa de clone é registrada sem derrubar automaticamente a máquina original;
- preços são autorizados pelo servidor;
- Access Token Mercado Pago fica cifrado no servidor e não é enviado ao gabinete;
- repositório privado: `https://github.com/luziellacerda/Servidor-pix`;
- branch: `main`;
- último commit confirmado com o pacote: `cee2127`.

## Pacote que deve ser usado

Arquivo no GitHub:

`outputs/TurboRamaPixOnlineServer-portable-52c8847.zip`

SHA-256 obrigatório:

`71af50e4d837d0e831277e4edd16fdedf1f242acb8f27b338b7ed17a275f994a`

O pacote não contém código-fonte, compilador, `.exe`, credenciais, chave privada ou estado de cliente.

## Primeiro passo no Linux: somente baixar e verificar

Não instalar ainda. Executar:

```bash
mkdir -p "$HOME/turborama-download"
cd "$HOME/turborama-download"
git clone --depth 1 --filter=blob:none --sparse https://github.com/luziellacerda/Servidor-pix.git
cd Servidor-pix
git sparse-checkout set outputs
ls -lh outputs/TurboRamaPixOnlineServer-portable-52c8847.zip
sha256sum outputs/TurboRamaPixOnlineServer-portable-52c8847.zip
```

O repositório é privado. Se o Git pedir senha, usar um token GitHub com acesso somente de leitura ao
repositório. Nunca colar o token na conversa.

## Inventário obrigatório antes da instalação

Executar e analisar os resultados sem modificar o servidor:

```bash
cat /etc/os-release
uname -m
git --version
dotnet --info
cloudflared --version
systemctl is-active cloudflared
systemctl is-enabled cloudflared
systemctl is-active nginx
systemctl is-active apache2
systemctl is-active caddy
sudo ss -lntp
df -h /
free -h
```

Não pedir `cat` de arquivos de ambiente, credenciais ou configuração integral do túnel, pois podem
conter segredos. Se for necessário examinar uma configuração, primeiro produzir uma versão sanitizada.

## Arquitetura de implantação planejada

- serviço separado do site existente;
- aplicação em `/opt/turborama-pix`;
- estado em `/var/lib/turborama-pix`;
- arquivo privado em `/etc/turborama-pix/server.env`, permissão `600`;
- usuário de serviço dedicado `turborama-pix`;
- escuta somente em `127.0.0.1:5187`;
- hostname novo no Cloudflare Tunnel existente;
- nenhuma porta pública nova;
- backup verificável antes de editar systemd ou Cloudflare.

Somente depois do inventário devem ser gerados comandos específicos para a distribuição Linux
detectada.

## Regras financeiras e de segurança

- credenciais Mercado Pago que apareceram na conversa antiga devem ser consideradas expostas;
- exigir credencial nova antes de qualquer cobrança real;
- a credencial nova deve ser digitada diretamente no servidor por entrada oculta;
- nunca solicitar token, senha, Client Secret ou chave privada pelo chat;
- não executar cobrança real durante instalação, diagnóstico ou autoteste;
- depois de tudo validado, ativar somente um gabinete e realizar teste real controlado de valor mínimo.

## Critério de continuidade

Ao receber os resultados do inventário, verificar primeiro compatibilidade do .NET, portas e estado do
Cloudflare/site. Depois preparar instalação reversível, com backup e rollback. Não alterar o sistema
existente por suposição.
