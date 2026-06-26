#pragma once

struct ID3D11Texture2D;

void InitOverlay();
void UpdateOverlay(struct ID3D11Texture2D* menuTex);
void ShutdownOverlay();
