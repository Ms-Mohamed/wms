import { Box, type BoxProps } from '@chakra-ui/react';

export default function Card(props: BoxProps) {
  return <Box bg="white" borderRadius="lg" boxShadow="card" overflow="hidden" {...props} />;
}
