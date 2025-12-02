"use client";

import { Button } from "@heroui/button";
import Link from "next/link";

type Props = {
  className?: string;
};

export default function AskQuestionButton({ className }: Props) {
  return (
    <Button
      as={Link}
      href="/questions/ask"
      color="secondary"
      className={className}
    >
      Ask Question
    </Button>
  );
}
