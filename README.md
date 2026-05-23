# BackupSave

Mod de backup e restauração para **My Summer Car** e **My Winter Car**.

O BackupSave cria backups compactados em ZIP, permite criar pontos de restauração permanentes e adiciona um gerenciador direto no menu principal do jogo.

## Recursos

- **Backup automático**: cria um backup ao carregar o save.
- **Restauração automática após morte**: detecta quando o save é apagado e restaura o backup mais recente.
- **Modos de restauração**: desligado, restaurar tudo ou restaurar mantendo as lápides.
- **Pontos de restauração**: backups permanentes que não entram no limite automático.
- **Backups em ZIP**: reduz arquivos soltos e mantém compatibilidade com backups antigos em pasta.
- **Limite de backups**: remove backups automáticos antigos quando passa do limite configurado.
- **Prefixo de personagem**: adiciona o primeiro nome do personagem ao backup.
- **Gerenciador no menu principal**: restaurar, deletar, criar ponto, abrir pastas e importar backups sem usar o menu do MSCLoader.
- **Ferramenta meshsave**: deleta `meshsave.txt` manualmente ou automaticamente no menu.
- **Importação de backups externos**: importa backups do SaveBackuper, MSC AutoBackup e MSC/MWC Save Backup Manager como pontos de restauração.
- **Importação MSC para MWC**: copia o save de My Summer Car para My Winter Car criando backup de segurança quando possível.
- **Backup de config do SatsumaTurboCharger**: no My Summer Car, quando o mod `SatsumaTurboCharger` está instalado, salva e restaura junto a pasta `Mods\Config\Mod Settings\SatsumaTurboCharger`.
- **Backup de config do MwcTurbocharger**: no My Winter Car, quando o mod `MwcTurbocharger` está instalado, salva e restaura junto a pasta `Mods\Config\Mod Settings\MwcTurbocharger`.
- **Suporte aos caminhos do MSCLoader**: as configs extras são procuradas na pasta `Mods` do jogo, em `Documentos\MySummerCar/MyWinterCar\Mods` e em `AppData\LocalLow\Amistech\My Summer Car/My Winter Car\Mods`.
- **Localização automática**: usa inglês por padrão e muda para português quando detecta o mod de tradução BR instalado.
- **Proteção do botão BACKUPS**: evita abrir o gerenciador quando o menu do MSCLoader está aberto por cima.

## Interface

No menu principal do jogo aparece o botão **BACKUPS**. Ele abre o gerenciador com:

- lista de backups e pontos de restauração;
- campo opcional para nomear ponto de restauração;
- botões para restaurar, deletar e criar ponto;
- botões para reiniciar o menu, abrir a pasta do save e abrir a pasta de backups;
- configurações de restauração automática, limite de backups e prefixo do personagem;
- opções de meshsave e importação quando disponíveis.

## Localização

O mod não usa `language.json`.

O idioma é detectado automaticamente pela presença do mod de tradução correspondente:

- **My Summer Car**: `MSC_Localization_Core_BR`
- **My Winter Car**: `MWC_Localization_Core_BR`

Sem esses mods, o BackupSave usa inglês.

## Pastas

Backups do BackupSave:

```text
C:\Users\{usuario}\AppData\LocalLow\Amistech\BackupSave
```

Save original do My Summer Car:

```text
C:\Users\{usuario}\AppData\LocalLow\Amistech\My Summer Car
```

Save original do My Winter Car:

```text
C:\Users\{usuario}\AppData\LocalLow\Amistech\My Winter Car
```

Backups antigos do SaveBackuper:

```text
C:\Users\{usuario}\AppData\LocalLow\Amistech\Backup
```

Backups do MSC/MWC Save Backup Manager:

```text
C:\Users\{usuario}\AppData\LocalLow\Amistech\My Summer Car\backups
C:\Users\{usuario}\AppData\LocalLow\Amistech\My Winter Car\backups
```

Backups e pontos do MSC AutoBackup:

```text
C:\Users\{usuario}\AppData\LocalLow\Amistech\Backup\MSC_Backup
C:\Users\{usuario}\AppData\LocalLow\Amistech\Backup\MWC_Backup
C:\Users\{usuario}\AppData\LocalLow\Amistech\RestorePoints\MSC_RestorePoints
C:\Users\{usuario}\AppData\LocalLow\Amistech\RestorePoints\MWC_RestorePoints
```

## Instalação

1. Copie `BackupSave.dll` para a pasta `Mods` do jogo.
2. Abra o jogo e use o botão **BACKUPS** no menu principal.

Uma cópia compilada fica em:

```text
MOD\BackupSave.dll
```

## Build

O projeto usa .NET Framework 3.5 e referências do jogo/MSCLoader.

O pós-build copia a DLL para as pastas de mods de My Summer Car e My Winter Car quando elas existem.

## Versão

**2.0.2**

Baseado originalmente no SaveBackuper por AnimeForevere.
