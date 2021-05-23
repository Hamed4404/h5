import { DotNet } from '@microsoft/dotnet-js-interop';

export function applyCaptureIdToElement(element: Element, referenceCaptureId: string) {
  element.setAttribute(getCaptureIdAttributeName(referenceCaptureId), '');
}

function getElementByCaptureId(referenceCaptureId: string) {
  const selector = `[${getCaptureIdAttributeName(referenceCaptureId)}]`;
  return document.querySelector(selector);
}

function getCaptureIdAttributeName(referenceCaptureId: string) {
  return `_bl_${referenceCaptureId}`;
}

// Support receiving ElementRef instances as args in interop calls
const elementRefKey = '__internalId'; // Keep in sync with ElementRef.cs
DotNet.attachReviver(function reviveElementReference(key: any, value: any, byteArrays: Uint8Array[] | null) {
  if (value && typeof value === 'object' && value.hasOwnProperty(elementRefKey) && typeof value[elementRefKey] === 'string') {
    return getElementByCaptureId(value[elementRefKey]);
  }

  // Unrecognized - let another reviver handle it
  return value;
});
