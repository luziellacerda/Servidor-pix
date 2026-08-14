# Implantação isolada no servidor Linux

Estes arquivos são exemplos. Eles **não devem substituir** a configuração do site ou do Cloudflare Tunnel que já está funcionando.

## Separação recomendada

- aplicação: `/opt/turborama-pix`;
- estado: `/var/lib/turborama-pix`;
- variáveis privadas: `/etc/turborama-pix/server.env` com proprietário `root` e permissão `600`;
- usuário dedicado sem login: `turborama-pix`;
- escuta somente em `127.0.0.1:5187`;
- hostname da API separado, por exemplo `pix-api.seudominio.com`;
- hostname administrativo separado, por exemplo `painel-pix.seudominio.com`, protegido pelo Cloudflare Access.

Não use a pasta do site atual, não compartilhe o usuário do site e não abra a porta 5187 no roteador ou firewall público.

## Cloudflare Tunnel existente

Faça backup do arquivo atual antes de qualquer edição. Acrescente apenas uma nova regra **antes** da regra final de erro ou catch-all:

```yaml
ingress:
  - hostname: pix-api.seudominio.com
    service: http://127.0.0.1:5187
  # mantenha aqui todas as regras que já existem
  - service: http_status:404
```

Não crie outro túnel se o túnel atual já atende ao servidor. Adicione um novo hostname ao mesmo túnel, valide a configuração e recarregue somente depois de confirmar que o site atual continua respondendo.

O hostname administrativo pode apontar para o mesmo `127.0.0.1:5187`, mas deve ser protegido pelo
Cloudflare Access. Não aplique essa regra humana ao hostname ou caminho usado pelos gabinetes em
`/v1/*`, pois o agente utiliza prova criptográfica própria. O servidor confia no
`X-Forwarded-Proto` somente quando ele chega do proxy local; isso permite cookies seguros mesmo com
o trecho interno do túnel em HTTP.

Mantenha `TURBORAMA_ADMIN_PUBLIC_HOST` vazio durante a instalação. Primeiro crie no Cloudflare
Access uma aplicação que proteja todo o hostname administrativo e habilite a validação do Access no
conector do túnel. Depois adicione a rota desse hostname ao túnel. Somente após confirmar a barreira
do Access, configure o hostname exato na variável e reinicie apenas o serviço TurboRama. O hostname
da API deve continuar recebendo `404` em qualquer caminho `/admin`.

## Arquivo privado de ambiente

Use `.env.example` apenas como referência. No servidor, gere duas chaves diferentes de 32 bytes e guarde-as fora do Git. Não copie os valores para conversa, issue ou terminal compartilhado.

O serviço de exemplo usa `/etc/turborama-pix/server.env`. Confirme as permissões antes de iniciar:

```text
proprietário: root
permissão: leitura e escrita somente para root (600)
```

Gere o hash da senha administrativa com `--hash-admin-password` em terminal privado. Coloque no
arquivo de ambiente apenas o hash. Nunca coloque a senha ou as chaves do estado no Git, no handoff
ou na conversa. A credencial Mercado Pago pertence ao gabinete e não deve ser enviada ao servidor
de licenciamento. O diretório configurado em
`TURBORAMA_ADMIN_KEY_DIRECTORY` precisa pertencer ao usuário do serviço e permanecer com permissão
restrita.

`TURBORAMA_ADMIN_PUBLIC_HOST` não aceita URL, porta, caminho ou curinga; informe somente o nome DNS
administrativo. Vazio significa que todas as rotas do painel permanecem desativadas.

## Ordem segura de ativação

1. Faça backup verificável do site, do túnel e das configurações atuais.
2. Instale o runtime ASP.NET Core 8 fornecido pela Microsoft.
3. Crie usuário, diretórios e permissões exclusivos do TurboRama.
4. Copie somente a saída compilada para `/opt/turborama-pix`.
5. Configure o arquivo privado de ambiente.
6. Execute o autoteste local.
7. Inicie o serviço apenas em `127.0.0.1:5187`.
8. Teste `/v1/health` localmente.
9. Confirme que `/admin` devolve `404` no hostname público da API.
10. Crie e valide a proteção Cloudflare Access para todo o hostname administrativo.
11. Adicione somente depois a rota administrativa ao túnel existente, sem remover regras atuais.
12. Configure `TURBORAMA_ADMIN_PUBLIC_HOST` e teste Access + login do TurboRama.
13. Teste o site antigo, a API e o painel antes de habilitar um gabinete.

## Importante

O serviço precisa de backup restaurado em teste, monitoramento e rotação de segredos antes do uso
comercial. O ensaio real do Mercado Pago é feito no gabinete; a implantação do servidor deve validar
licença, máquina, painel, auditoria e isolamento sem criar cobrança.
