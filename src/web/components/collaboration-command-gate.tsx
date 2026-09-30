"use client";

import { createContext, type ReactNode, useContext } from "react";

interface CollaborationCommandGate {
  readonly canCommand: boolean;
  readonly isCurrent: () => boolean;
  readonly canDraft: boolean;
  readonly isAuthorized: () => boolean;
}

const denied = () => false;
const context = createContext<CollaborationCommandGate>({
  canCommand: false, isCurrent: denied, canDraft: false, isAuthorized: denied,
});

export function CollaborationCommandBoundary({ value, children }: {
  readonly value: CollaborationCommandGate; readonly children: ReactNode;
}) {
  return <context.Provider value={value}>{children}</context.Provider>;
}

export function useCollaborationCommandGate() { return useContext(context); }
