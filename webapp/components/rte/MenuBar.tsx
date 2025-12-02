"use client";

import { Editor } from "@tiptap/core";
import { useEditorState } from "@tiptap/react";
import {
  BoldIcon,
  CodeBracketIcon,
  ItalicIcon,
  LinkIcon,
  PhotoIcon,
  StrikethroughIcon,
} from "@heroicons/react/20/solid";
import { Button } from "@heroui/button";
import {
  CldUploadButton,
  CloudinaryUploadWidgetResults,
} from "next-cloudinary";
import { errorToast } from "@/lib/util";

type Props = {
  editor: Editor | null;
};

export default function MenuBar({ editor }: Props) {
  const editorState = useEditorState({
    editor,
    selector: ({ editor }) => {
      if (!editor) return null;

      return {
        isBold: editor.isActive("bold"),
        isItalic: editor.isActive("italic"),
        isStrike: editor.isActive("strike"),
        isCodeBlock: editor.isActive("codeBlock"),
        isLink: editor.isActive("link"),
      };
    },
  });

  if (!editor || !editorState) return null;

  const onUploadImage = async (result: CloudinaryUploadWidgetResults) => {
    try {
      if (
        result.info &&
        typeof result.info === "object" &&
        "secure_url" in result.info
      ) {
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        const secureUrl = (result.info as any).secure_url as string;

        // 1) Insert image into the editor
        editor.chain().focus().setImage({ src: secureUrl }).run();

        // 2) Ask Groq for an error description based on the image
        const res = await fetch("/api/groq-image-description", {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ imageUrl: secureUrl }),
        });
        console.log("Groq description response", res);
        if (!res.ok) {
          console.error("Groq description request failed", await res.text());
          errorToast({
            message: "Problem generating description for the image",
          });
          return;
        }

        const data = (await res.json()) as { description?: string };
        const description = data.description?.trim();

        if (description) {
          // 3) Insert a new paragraph with the description AFTER the image without replacing it
          const pos = editor.state.selection.to;
          editor
            .chain()
            .focus()
            .insertContentAt(pos, {
              type: "paragraph",
              content: [{ type: "text", text: description }],
            })
            .run();
        }
      } else {
        errorToast({ message: "Problem adding image" });
      }
    } catch (err) {
      console.error("Error while uploading/describing image", err);
      errorToast({ message: "Unexpected error generating image description" });
    }
  };

  const options = [
    {
      icon: <BoldIcon className="w-5 h-5" />,
      onClick: () => editor.chain().focus().toggleBold().run(),
      pressed: editorState.isBold,
    },
    {
      icon: <ItalicIcon className="w-5 h-5" />,
      onClick: () => editor.chain().focus().toggleItalic().run(),
      pressed: editorState.isItalic,
    },
    {
      icon: <StrikethroughIcon className="w-5 h-5" />,
      onClick: () => editor.chain().focus().toggleStrike().run(),
      pressed: editorState.isStrike,
    },
    {
      icon: <CodeBracketIcon className="w-5 h-5" />,
      onClick: () => editor.chain().focus().toggleCodeBlock().run(),
      pressed: editorState.isCodeBlock,
    },
    {
      icon: <LinkIcon className="w-5 h-5" />,
      onClick: () => editor.chain().focus().toggleLink().run(),
      pressed: editorState.isLink,
    },
  ];

  return (
    <div className="rounded-md space-x-1 pb-1 z-50">
      {options.map((option, index) => (
        <Button
          key={index}
          type="button"
          radius="sm"
          size="sm"
          isIconOnly
          color={option.pressed ? "primary" : "default"}
          onPress={option.onClick}
        >
          {option.icon}
        </Button>
      ))}
      <Button
        isIconOnly
        size="sm"
        as={CldUploadButton}
        options={{ maxFiles: 1 }}
        onSuccess={onUploadImage}
        signatureEndpoint="/api/sign-image"
        uploadPreset="overflow"
      >
        <PhotoIcon className="w-5 h-5" />
      </Button>
    </div>
  );
}
